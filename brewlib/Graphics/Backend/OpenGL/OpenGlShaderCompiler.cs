namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shaders;
using Silk.NET.SPIRV.Cross;
using CrossCompiler = Silk.NET.SPIRV.Cross.Compiler;
using CrossResult = Silk.NET.SPIRV.Cross.Result;

static unsafe class OpenGlShaderCompiler
{
    public static ShaderProgramSource CreateShaderSource(RenderPipelineDescription description,
        uint glslVersion,
        bool glslEs)
    {
        var source = description.ShaderSource;
        if (source.Language == ShaderSourceLanguage.Glsl)
            return source;

        if (source.Language != ShaderSourceLanguage.Hlsl)
            throw new NotSupportedException($"OpenGL cannot compile {source.Language} shader sources");

        string vertexSource = null, fragmentSource = null;
        SpirVShaderMetadata vertexMetadata = null, fragmentMetadata = null;
        Parallel.Invoke(
            () => (vertexSource, vertexMetadata) = createVertexSource(description, glslVersion, glslEs),
            () => (fragmentSource, fragmentMetadata) = createFragmentSource(description, glslVersion, glslEs));

        return new(source.Name,
            vertexSource,
            fragmentSource,
            ShaderSourceLanguage.Glsl,
            UniformBlocks: getUniformBlocks(vertexMetadata, fragmentMetadata));
    }

    public static ShaderProgramSource CreateShaderSource(ShaderProgramSource source,
        uint glslVersion,
        bool glslEs)
    {
        if (source.Language == ShaderSourceLanguage.Glsl)
            return source;

        if (source.Language != ShaderSourceLanguage.Hlsl)
            throw new NotSupportedException($"OpenGL cannot compile {source.Language} shader sources");

        (byte[] SpirV, SpirVShaderMetadata Metadata) vertexCompiled = default, fragmentCompiled = default;
        Parallel.Invoke(
            () => vertexCompiled = SpirVShaderCompiler.CompileAndReflectSpirVFromHlsl(source.Name + ".Vertex",
                CompiledShaderStage.Vertex,
                source.VertexSource,
                source.VertexEntryPoint),
            () => fragmentCompiled = SpirVShaderCompiler.CompileAndReflectSpirVFromHlsl(source.Name + ".Fragment",
                CompiledShaderStage.Fragment,
                source.FragmentSource,
                source.FragmentEntryPoint));

        var stageInterfaceNames = getStageInterfaceNames(vertexCompiled.Metadata, fragmentCompiled.Metadata);
        string vertexSource = null, fragmentSource = null;
        Parallel.Invoke(
            () => vertexSource = createVertexSource(source, glslVersion, glslEs, vertexCompiled, stageInterfaceNames),
            () => fragmentSource = createFragmentSource(source, glslVersion, glslEs, fragmentCompiled, stageInterfaceNames));

        return new(source.Name, vertexSource, fragmentSource, ShaderSourceLanguage.Glsl);
    }

    static (string Source, SpirVShaderMetadata Metadata) createVertexSource(RenderPipelineDescription description,
        uint glslVersion,
        bool glslEs)
    {
        var source = description.ShaderSource;
        var compiled = SpirVShaderCompiler.CompileAndReflectSpirVFromHlsl(source.Name + ".Vertex",
            CompiledShaderStage.Vertex,
            source.VertexSource,
            source.VertexEntryPoint);

        var stageInterfaceNames = getStageInterfaceNames(compiled.Metadata, null);
        var glsl = SpirVShaderCompiler.CompileGlslFromSpirV(compiled.SpirV,
            CompiledShaderStage.Vertex,
            source.VertexEntryPoint,
            glslVersion,
            (cross, compiler, _) =>
            {
                renameVertexInputs(cross, compiler, compiled.Metadata, description.VertexInput);
                renameStageOutputs(cross, compiler, compiled.Metadata, stageInterfaceNames);
                renameUniformBuffers(cross, compiler, compiled.Metadata);
            },
            false,
            glslEs);

        return (glsl, compiled.Metadata);
    }

    static (string Source, SpirVShaderMetadata Metadata) createFragmentSource(RenderPipelineDescription description,
        uint glslVersion,
        bool glslEs)
    {
        var source = description.ShaderSource;
        var compiled = SpirVShaderCompiler.CompileAndReflectSpirVFromHlsl(source.Name + ".Fragment",
            CompiledShaderStage.Fragment,
            source.FragmentSource,
            source.FragmentEntryPoint);

        var glsl = SpirVShaderCompiler.CompileGlslFromSpirV(compiled.SpirV,
            CompiledShaderStage.Fragment,
            source.FragmentEntryPoint,
            glslVersion,
            (cross, compiler, context) =>
            {
                renameStageInputs(cross, compiler, compiled.Metadata, getStageInterfaceNames(null, compiled.Metadata));
                renameTextureResources(cross, compiler, context, compiled.Metadata, description.PipelineLayout);
                renameUniformBuffers(cross, compiler, compiled.Metadata);
            },
            false,
            glslEs);

        return (glsl, compiled.Metadata);
    }

    static string createVertexSource(ShaderProgramSource source,
        uint glslVersion,
        bool glslEs,
        (byte[] SpirV, SpirVShaderMetadata Metadata) compiled,
        IReadOnlyDictionary<uint, string> stageInterfaceNames)
    {
        return SpirVShaderCompiler.CompileGlslFromSpirV(compiled.SpirV,
            CompiledShaderStage.Vertex,
            source.VertexEntryPoint,
            glslVersion,
            (cross, compiler, _) =>
            {
                renameVertexInputs(cross, compiler, compiled.Metadata, source.VertexInputNames);
                renameStageOutputs(cross, compiler, compiled.Metadata, stageInterfaceNames);
            },
            glslEs: glslEs);
    }

    static string createFragmentSource(ShaderProgramSource source,
        uint glslVersion,
        bool glslEs,
        (byte[] SpirV, SpirVShaderMetadata Metadata) compiled,
        IReadOnlyDictionary<uint, string> stageInterfaceNames)
    {
        return SpirVShaderCompiler.CompileGlslFromSpirV(compiled.SpirV,
            CompiledShaderStage.Fragment,
            source.FragmentEntryPoint,
            glslVersion,
            (cross, compiler, context) =>
            {
                renameStageInputs(cross, compiler, compiled.Metadata, stageInterfaceNames);
                renameTextureResources(cross, compiler, context, compiled.Metadata, source.FragmentTextureNames);
            },
            glslEs: glslEs);
    }

    static void renameVertexInputs(Cross cross,
        CrossCompiler* compiler,
        SpirVShaderMetadata metadata,
        VertexInputLayout vertexInput)
    {
        var namesByLocation = getVertexInputNames(vertexInput);
        foreach (var input in metadata.Inputs)
            if (namesByLocation.TryGetValue(input.Location, out var name))
                cross.CompilerSetName(compiler, input.SpirvId, name);
    }

    static void renameVertexInputs(Cross cross,
        CrossCompiler* compiler,
        SpirVShaderMetadata metadata,
        IReadOnlyList<string> namesByLocation)
    {
        if (namesByLocation is null) return;

        foreach (var input in metadata.Inputs)
            if (input.Location < namesByLocation.Count && namesByLocation[(int)input.Location] is { } name)
                cross.CompilerSetName(compiler, input.SpirvId, name);
    }

    static void renameTextureResources(Cross cross,
        CrossCompiler* compiler,
        Context* context,
        SpirVShaderMetadata metadata,
        PipelineLayout pipelineLayout)
    {
        foreach (var descriptor in metadata.Descriptors)
        {
            if (descriptor.Kind is not (ShaderDescriptorKind.SampledImage or ShaderDescriptorKind.CombinedImageSampler))
                continue;

            if (tryGetTextureBindingName(pipelineLayout, descriptor.Binding, out var name))
                cross.CompilerSetName(compiler, descriptor.SpirvId, name);
        }

        check(cross.CompilerBuildCombinedImageSamplers(compiler),
            context,
            cross,
            "build GLSL combined image samplers");

        CombinedImageSampler* combinedSamplers = null;
        nuint combinedSamplerCount = 0;
        check(cross.CompilerGetCombinedImageSamplers(compiler, &combinedSamplers, &combinedSamplerCount),
            context,
            cross,
            "reflect GLSL combined image samplers");

        for (nuint i = 0; i < combinedSamplerCount; ++i)
        {
            ref var combinedSampler = ref combinedSamplers[i];
            if (tryGetTextureBindingName(metadata, pipelineLayout, combinedSampler.ImageId, out var name) ||
                tryGetTextureBindingName(metadata, pipelineLayout, combinedSampler.SamplerId, out name))
                cross.CompilerSetName(compiler, combinedSampler.CombinedId, name);
        }
    }

    static void renameTextureResources(Cross cross,
        CrossCompiler* compiler,
        Context* context,
        SpirVShaderMetadata metadata,
        IReadOnlyList<string> namesByBinding)
    {
        if (namesByBinding is null) return;

        foreach (var descriptor in metadata.Descriptors)
        {
            if (descriptor.Kind is not (ShaderDescriptorKind.SampledImage or ShaderDescriptorKind.CombinedImageSampler))
                continue;

            if (descriptor.Binding < namesByBinding.Count && namesByBinding[(int)descriptor.Binding] is { } name)
                cross.CompilerSetName(compiler, descriptor.SpirvId, name);
        }

        check(cross.CompilerBuildCombinedImageSamplers(compiler),
            context,
            cross,
            "build GLSL combined image samplers");

        CombinedImageSampler* combinedSamplers = null;
        nuint combinedSamplerCount = 0;
        check(cross.CompilerGetCombinedImageSamplers(compiler, &combinedSamplers, &combinedSamplerCount),
            context,
            cross,
            "reflect GLSL combined image samplers");

        for (nuint i = 0; i < combinedSamplerCount; ++i)
        {
            ref var combinedSampler = ref combinedSamplers[i];
            if (tryGetTextureBindingName(metadata, namesByBinding, combinedSampler.ImageId, out var name) ||
                tryGetTextureBindingName(metadata, namesByBinding, combinedSampler.SamplerId, out name))
                cross.CompilerSetName(compiler, combinedSampler.CombinedId, name);
        }
    }

    static void renameStageOutputs(Cross cross,
        CrossCompiler* compiler,
        SpirVShaderMetadata metadata,
        IReadOnlyDictionary<uint, string> namesByLocation)
    {
        foreach (var output in metadata.Outputs)
            if (namesByLocation.TryGetValue(output.Location, out var name))
                cross.CompilerSetName(compiler, output.SpirvId, name);
    }

    static void renameStageInputs(Cross cross,
        CrossCompiler* compiler,
        SpirVShaderMetadata metadata,
        IReadOnlyDictionary<uint, string> namesByLocation)
    {
        foreach (var input in metadata.Inputs)
            if (namesByLocation.TryGetValue(input.Location, out var name))
                cross.CompilerSetName(compiler, input.SpirvId, name);
    }

    static Dictionary<uint, string> getVertexInputNames(VertexInputLayout vertexInput)
    {
        Dictionary<uint, string> namesByLocation = [];
        uint location = 0;
        foreach (var buffer in vertexInput.Buffers)
        foreach (var element in buffer.Elements)
        {
            namesByLocation[location] = element.Name;
            location += (uint)element.Format.GetLocationCount();
        }

        return namesByLocation;
    }

    static Dictionary<uint, string> getStageInterfaceNames(SpirVShaderMetadata vertexMetadata,
        SpirVShaderMetadata fragmentMetadata)
    {
        Dictionary<uint, string> namesByLocation = [];

        if (vertexMetadata is not null)
            foreach (var output in vertexMetadata.Outputs)
                namesByLocation.TryAdd(output.Location, getStageInterfaceName(output.Location));

        if (fragmentMetadata is not null)
            foreach (var input in fragmentMetadata.Inputs)
                namesByLocation.TryAdd(input.Location, getStageInterfaceName(input.Location));

        return namesByLocation;
    }

    static string getStageInterfaceName(uint location)
        => "v_location" + location;

    static IReadOnlyList<ShaderUniformBlockBinding> getUniformBlocks(SpirVShaderMetadata vertexMetadata,
        SpirVShaderMetadata fragmentMetadata)
    {
        List<ShaderUniformBlockBinding> blocks = [];
        addUniformBlocks(blocks, vertexMetadata, ShaderBindingStage.Vertex);
        addUniformBlocks(blocks, fragmentMetadata, ShaderBindingStage.Fragment);
        return blocks;
    }

    static void addUniformBlocks(List<ShaderUniformBlockBinding> blocks,
        SpirVShaderMetadata metadata,
        ShaderBindingStage stage)
    {
        if (metadata is null) return;

        foreach (var descriptor in metadata.Descriptors)
            if (descriptor.Kind == ShaderDescriptorKind.UniformBuffer)
                blocks.Add(new(getUniformBufferName(descriptor.Binding), stage, descriptor.Binding));
    }

    static void renameUniformBuffers(Cross cross, CrossCompiler* compiler, SpirVShaderMetadata metadata)
    {
        foreach (var descriptor in metadata.Descriptors)
            if (descriptor.Kind == ShaderDescriptorKind.UniformBuffer)
                cross.CompilerSetName(compiler, descriptor.SpirvId, getUniformBufferName(descriptor.Binding));
    }

    static string getUniformBufferName(uint binding)
        => "u_uniformBlock" + binding;

    static bool tryGetTextureBindingName(PipelineLayout pipelineLayout, uint binding, out string name)
    {
        foreach (var textureBinding in pipelineLayout.TextureBindings)
            if (textureBinding.Binding == binding)
            {
                name = textureBinding.Name;
                return true;
            }

        name = null;
        return false;
    }

    static bool tryGetTextureBindingName(SpirVShaderMetadata metadata,
        PipelineLayout pipelineLayout,
        uint spirvId,
        out string name)
    {
        foreach (var descriptor in metadata.Descriptors)
            if (descriptor.SpirvId == spirvId)
                return tryGetTextureBindingName(pipelineLayout, descriptor.Binding, out name);

        name = null;
        return false;
    }

    static bool tryGetTextureBindingName(SpirVShaderMetadata metadata,
        IReadOnlyList<string> namesByBinding,
        uint spirvId,
        out string name)
    {
        foreach (var descriptor in metadata.Descriptors)
            if (descriptor.SpirvId == spirvId &&
                descriptor.Binding < namesByBinding.Count &&
                namesByBinding[(int)descriptor.Binding] is { } bindingName)
            {
                name = bindingName;
                return true;
            }

        name = null;
        return false;
    }

    static void check(CrossResult result, Context* context, Cross cross, string operation)
    {
        if (result == CrossResult.Success) return;

        var detail = context is null ? string.Empty : cross.ContextGetLastErrorStringS(context);
        throw new InvalidOperationException($"Unable to {operation}: {result} {detail}");
    }
}