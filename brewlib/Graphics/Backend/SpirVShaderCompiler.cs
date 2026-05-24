namespace BrewLib.Graphics.Backend;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Shaders;
using Silk.NET.Shaderc;
using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Cross;
using Silk.NET.SPIRV.Reflect;
using CrossCompiler = Silk.NET.SPIRV.Cross.Compiler;
using CrossBackend = Silk.NET.SPIRV.Cross.Backend;
using CrossResult = Silk.NET.SPIRV.Cross.Result;
using ReflectDescriptorType = Silk.NET.SPIRV.Reflect.DescriptorType;
using ReflectApi = Silk.NET.SPIRV.Reflect.Reflect;
using ReflectResult = Silk.NET.SPIRV.Reflect.Result;
using ShadercSourceLanguage = Silk.NET.Shaderc.SourceLanguage;

static class SpirVShaderCompiler
{
    public static (byte[] SpirV, SpirVShaderMetadata Metadata) CompileAndReflectSpirVFromHlsl(string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint = "main",
        bool enableDebug = false)
    {
        var spirV = CompileSpirVFromHlsl(name, stage, source, entryPoint, enableDebug);
        return (spirV, Reflect(spirV));
    }

    public static byte[] CompileSpirVFromHlsl(string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint = "main",
        bool enableDebug = false)
        => compileSpirV(name, stage, source, entryPoint, enableDebug, ShadercSourceLanguage.Hlsl);

    public static byte[] CompileSpirVFromGlsl(string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint = "main",
        bool enableDebug = false)
        => compileSpirV(name, stage, source, entryPoint, enableDebug, ShadercSourceLanguage.Glsl);

    static unsafe byte[] compileSpirV(string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint,
        bool enableDebug,
        ShadercSourceLanguage sourceLanguage)
    {
        var shaderc = Shaderc.GetApi();
        var compiler = shaderc.CompilerInitialize();
        if (compiler is null)
            throw new InvalidOperationException("Unable to initialize shaderc compiler");

        var options = shaderc.CompileOptionsInitialize();
        if (options is null)
        {
            shaderc.CompilerRelease(compiler);
            throw new InvalidOperationException("Unable to initialize shaderc compile options");
        }

        try
        {
            shaderc.CompileOptionsSetSourceLanguage(options, sourceLanguage);
            shaderc.CompileOptionsSetTargetEnv(options, TargetEnv.Vulkan, (uint)EnvVersion.Vulkan12);
            shaderc.CompileOptionsSetOptimizationLevel(options,
                enableDebug ? OptimizationLevel.Zero : OptimizationLevel.Performance);

            if (sourceLanguage == ShadercSourceLanguage.Hlsl)
            {
                shaderc.CompileOptionsSetHlslIoMapping(options, true);
                shaderc.CompileOptionsSetHlslOffsets(options, true);
                shaderc.CompileOptionsSetAutoCombinedImageSampler(options, true);
            }

            if (enableDebug) shaderc.CompileOptionsSetGenerateDebugInfo(options);

            var result = shaderc.CompileIntoSpv(compiler,
                source,
                (nuint)Encoding.UTF8.GetByteCount(source),
                toShadercKind(stage),
                name,
                entryPoint,
                options);

            if (result is null)
                throw new InvalidOperationException($"Unable to compile HLSL shader {name} to SPIR-V");

            try
            {
                var status = shaderc.ResultGetCompilationStatus(result);
                if (status != CompilationStatus.Success)
                {
                    var error = Marshal.PtrToStringUTF8((nint)shaderc.ResultGetErrorMessage(result));
                    throw new InvalidOperationException(
                        $"Unable to compile HLSL shader {name} to SPIR-V: {status}\n{error}");
                }

                var length = checked((int)shaderc.ResultGetLength(result));
                var bytecode = new byte[length];
                Marshal.Copy((nint)shaderc.ResultGetBytes(result), bytecode, 0, bytecode.Length);
                return bytecode;
            }
            finally
            {
                shaderc.ResultRelease(result);
            }
        }
        finally
        {
            shaderc.CompileOptionsRelease(options);
            shaderc.CompilerRelease(compiler);
        }
    }

    public static unsafe SpirVShaderMetadata Reflect(byte[] spirV)
    {
        if (spirV.Length == 0 || spirV.Length % sizeof(uint) != 0)
            throw new ArgumentException("SPIR-V bytecode must be a non-empty uint32 stream", nameof(spirV));

        var reflect = ReflectApi.GetApi();
        fixed (byte* bytes = spirV)
        {
            ReflectShaderModule module = default;
            check(reflect.CreateShaderModule((UIntPtr)spirV.Length, bytes, &module), "create SPIR-V reflection module");
            try
            {
                var inputs = new List<ShaderInputMetadata>(checked((int)module.InputVariableCount));
                for (uint i = 0; i < module.InputVariableCount; ++i)
                {
                    var variable = module.InputVariables[i];
                    if (variable is null || variable->BuiltIn >= 0) continue;

                    inputs.Add(new(variable->SpirvId,
                        variable->Location,
                        readUtf8(variable->Name),
                        readUtf8(variable->Semantic)));
                }

                var outputs = new List<ShaderInputMetadata>(checked((int)module.OutputVariableCount));
                for (uint i = 0; i < module.OutputVariableCount; ++i)
                {
                    var variable = module.OutputVariables[i];
                    if (variable is null || variable->BuiltIn >= 0) continue;

                    outputs.Add(new(variable->SpirvId,
                        variable->Location,
                        readUtf8(variable->Name),
                        readUtf8(variable->Semantic)));
                }

                var descriptors = new List<ShaderDescriptorMetadata>(checked((int)module.DescriptorBindingCount));
                uint combinedImageSamplers = 0, sampledImages = 0, samplers = 0;
                uint storageTextures = 0, storageBuffers = 0, uniformBuffers = 0;

                for (uint i = 0; i < module.DescriptorBindingCount; ++i)
                {
                    ref var binding = ref module.DescriptorBindings[i];
                    var count = getDescriptorCount(in binding);
                    var kind = toDescriptorKind(binding.DescriptorType);

                    descriptors.Add(new(binding.SpirvId,
                        binding.Binding,
                        binding.Set,
                        readUtf8(binding.Name),
                        kind,
                        count));

                    switch (kind)
                    {
                        case ShaderDescriptorKind.CombinedImageSampler:
                            combinedImageSamplers = checked(combinedImageSamplers + count);
                            break;

                        case ShaderDescriptorKind.SampledImage:
                            sampledImages = checked(sampledImages + count);
                            break;

                        case ShaderDescriptorKind.Sampler:
                            samplers = checked(samplers + count);
                            break;

                        case ShaderDescriptorKind.StorageTexture:
                            storageTextures = checked(storageTextures + count);
                            break;

                        case ShaderDescriptorKind.StorageBuffer:
                            storageBuffers = checked(storageBuffers + count);
                            break;

                        case ShaderDescriptorKind.UniformBuffer:
                            uniformBuffers = checked(uniformBuffers + count);
                            break;
                    }
                }

                return new(readUtf8(module.EntryPointName),
                    inputs.ToArray(),
                    outputs.ToArray(),
                    descriptors.ToArray(),
                    new(uint.Max(combinedImageSamplers, uint.Max(sampledImages, samplers)),
                        storageTextures,
                        storageBuffers,
                        uniformBuffers));
            }
            finally
            {
                reflect.DestroyShaderModule(&module);
            }
        }
    }

    public static unsafe string CompileGlslFromSpirV(byte[] spirV,
        CompiledShaderStage stage,
        string entryPoint,
        uint glslVersion,
        SpirVGlslCompilerConfigurator configure = null,
        bool emitUniformBuffersAsPlainUniforms = true,
        bool glslEs = false)
    {
        if (spirV.Length == 0 || spirV.Length % sizeof(uint) != 0)
            throw new ArgumentException("SPIR-V bytecode must be a non-empty uint32 stream", nameof(spirV));

        var cross = Cross.GetApi();
        Context* context = null;

        fixed (byte* bytes = spirV)
        {
            check(cross.ContextCreate(&context), context, cross, "create SPIRV-Cross context");
            try
            {
                ParsedIr* parsedIr = null;
                check(cross.ContextParseSpirv(context,
                        (uint*)bytes,
                        (nuint)(spirV.Length / sizeof(uint)),
                        &parsedIr),
                    context,
                    cross,
                    "parse SPIR-V");

                CrossCompiler* compiler = null;
                check(cross.ContextCreateCompiler(context,
                        CrossBackend.Glsl,
                        parsedIr,
                        CaptureMode.Copy,
                        &compiler),
                    context,
                    cross,
                    "create SPIRV-Cross GLSL compiler");

                check(cross.CompilerSetEntryPoint(compiler, entryPoint, toExecutionModel(stage)),
                    context,
                    cross,
                    $"select SPIR-V entry point {entryPoint}");

                CompilerOptions* options = null;
                check(cross.CompilerCreateCompilerOptions(compiler, &options),
                    context,
                    cross,
                    "create SPIRV-Cross GLSL options");

                check(cross.CompilerOptionsSetUint(options, CompilerOption.GlslVersion, glslVersion),
                    context,
                    cross,
                    "set GLSL version");

                check(cross.CompilerOptionsSetBool(options, CompilerOption.GlslES, glslEs ? (byte)1 : (byte)0),
                    context,
                    cross,
                    glslEs ? "enable GLSL ES output" : "disable GLSL ES output");

                check(cross.CompilerOptionsSetBool(options, CompilerOption.GlslVulkanSemantics, 0),
                    context,
                    cross,
                    "disable GLSL Vulkan semantics");

                check(cross.CompilerOptionsSetBool(options,
                        CompilerOption.GlslEmitUniformBufferAsPlainUniforms,
                        emitUniformBuffersAsPlainUniforms ? (byte)1 : (byte)0),
                    context,
                    cross,
                    "emit uniform buffers as plain uniforms");

                check(cross.CompilerInstallCompilerOptions(compiler, options),
                    context,
                    cross,
                    "install SPIRV-Cross GLSL options");

                configure?.Invoke(cross, compiler, context);

                byte* source = null;
                check(cross.CompilerCompile(compiler, &source), context, cross, "compile SPIR-V to GLSL");
                return Marshal.PtrToStringUTF8((nint)source) ??
                    throw new InvalidOperationException("SPIRV-Cross returned a null GLSL source");
            }
            finally
            {
                if (context is not null) cross.ContextDestroy(context);
            }
        }
    }

    static uint getDescriptorCount(scoped ref readonly DescriptorBinding binding)
        => binding.Count == 0 ? 1u : binding.Count;

    static ShaderDescriptorKind toDescriptorKind(ReflectDescriptorType descriptorType)
        => descriptorType switch
        {
            ReflectDescriptorType.Sampler => ShaderDescriptorKind.Sampler,
            ReflectDescriptorType.CombinedImageSampler => ShaderDescriptorKind.CombinedImageSampler,
            ReflectDescriptorType.SampledImage => ShaderDescriptorKind.SampledImage,
            ReflectDescriptorType.StorageImage => ShaderDescriptorKind.StorageTexture,
            ReflectDescriptorType.UniformBuffer or ReflectDescriptorType.UniformBufferDynamic => ShaderDescriptorKind.UniformBuffer,
            ReflectDescriptorType.StorageBuffer or ReflectDescriptorType.StorageBufferDynamic => ShaderDescriptorKind.StorageBuffer,
            _ => ShaderDescriptorKind.Other
        };

    static ShaderKind toShadercKind(CompiledShaderStage stage)
        => stage switch
        {
            CompiledShaderStage.Vertex => ShaderKind.VertexShader,
            CompiledShaderStage.Fragment => ShaderKind.FragmentShader,
            CompiledShaderStage.Compute => ShaderKind.ComputeShader,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };

    static ExecutionModel toExecutionModel(CompiledShaderStage stage)
        => stage switch
        {
            CompiledShaderStage.Vertex => ExecutionModel.Vertex,
            CompiledShaderStage.Fragment => ExecutionModel.Fragment,
            CompiledShaderStage.Compute => ExecutionModel.GLCompute,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };

    static unsafe string readUtf8(byte* value)
        => value is null ? string.Empty : Marshal.PtrToStringUTF8((nint)value) ?? string.Empty;

    static void check(ReflectResult result, string operation)
    {
        if (result == ReflectResult.Success) return;

        throw new InvalidOperationException($"Unable to {operation}: {result}");
    }

    static unsafe void check(CrossResult result, Context* context, Cross cross, string operation)
    {
        if (result == CrossResult.Success) return;

        var detail = context is null ? string.Empty : cross.ContextGetLastErrorStringS(context);
        throw new InvalidOperationException($"Unable to {operation}: {result} {detail}");
    }
}

unsafe delegate void SpirVGlslCompilerConfigurator(Cross cross, CrossCompiler* compiler, Context* context);

sealed class SpirVShaderMetadata(
    string entryPoint,
    ShaderInputMetadata[] inputs,
    ShaderInputMetadata[] outputs,
    ShaderDescriptorMetadata[] descriptors,
    ShaderResourceMetadata resources)
{
    public string EntryPoint { get; } = entryPoint;
    public ReadOnlySpan<ShaderInputMetadata> Inputs => inputs;
    public ReadOnlySpan<ShaderInputMetadata> Outputs => outputs;
    public ReadOnlySpan<ShaderDescriptorMetadata> Descriptors => descriptors;
    public ShaderResourceMetadata Resources { get; } = resources;
}

readonly record struct ShaderInputMetadata(
    uint SpirvId,
    uint Location,
    string Name,
    string Semantic);

readonly record struct ShaderDescriptorMetadata(
    uint SpirvId,
    uint Binding,
    uint Set,
    string Name,
    ShaderDescriptorKind Kind,
    uint Count);

enum ShaderDescriptorKind
{
    Other,
    Sampler,
    CombinedImageSampler,
    SampledImage,
    StorageTexture,
    StorageBuffer,
    UniformBuffer
}

readonly record struct ShaderResourceMetadata(
    uint Samplers,
    uint StorageTextures,
    uint StorageBuffers,
    uint UniformBuffers);