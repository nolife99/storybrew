namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SDL3;
using Shaders;

public static class SdlShaderCompiler
{
    public static SDL.GPUShaderFormat SupportedShaderFormats
        => SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.DXBC;

    public static SDL.GPUShaderFormat SelectShaderFormat(SDL.GPUShaderFormat supportedFormats)
    {
        if (hasFormat(supportedFormats, SDL.GPUShaderFormat.DXBC))
            return SDL.GPUShaderFormat.DXBC;

        if (hasFormat(supportedFormats, SDL.GPUShaderFormat.SPIRV))
            return SDL.GPUShaderFormat.SPIRV;

        throw new NotSupportedException($"SDL GPU device does not support a shader format we can compile: {supportedFormats}");
    }

    public static byte[] CompileSpirVFromHlsl(string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint = "main",
        bool enableDebug = false)
        => SpirVShaderCompiler.CompileSpirVFromHlsl(name, stage, source, entryPoint, enableDebug);

    public static (nint VertexShader, nint FragmentShader) CompileGraphicsShadersFromHlsl(nint device,
        SDL.GPUShaderFormat shaderFormat,
        ShaderProgramSource source,
        bool enableDebug = false)
    {
        nint vertexShader = 0, fragmentShader = 0;

        try
        {
            Parallel.Invoke(
                () => vertexShader = CompileGraphicsShaderFromHlsl(device,
                    shaderFormat,
                    source.Name + ".Vertex",
                    CompiledShaderStage.Vertex,
                    source.VertexSource,
                    source.VertexEntryPoint,
                    enableDebug),
                () => fragmentShader = CompileGraphicsShaderFromHlsl(device,
                    shaderFormat,
                    source.Name + ".Fragment",
                    CompiledShaderStage.Fragment,
                    source.FragmentSource,
                    source.FragmentEntryPoint,
                    enableDebug));

            return (vertexShader, fragmentShader);
        }
        catch
        {
            if (vertexShader != 0) SDL.ReleaseGPUShader(device, vertexShader);
            if (fragmentShader != 0) SDL.ReleaseGPUShader(device, fragmentShader);
            throw;
        }
    }

    public static nint CompileGraphicsShaderFromHlsl(nint device,
        SDL.GPUShaderFormat shaderFormat,
        string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint = "main",
        bool enableDebug = false)
        => shaderFormat switch
        {
            SDL.GPUShaderFormat.SPIRV => CompileGraphicsShaderFromSpirV(device,
                name,
                stage,
                CompileSpirVFromHlsl(name, stage, source, entryPoint, enableDebug),
                entryPoint),

            SDL.GPUShaderFormat.DXBC => CompileGraphicsShaderFromDxbc(device,
                name,
                stage,
                source,
                entryPoint,
                enableDebug),

            _ => throw new NotSupportedException($"SDL shader format {shaderFormat} is not supported by the runtime compiler")
        };

    public static nint CompileGraphicsShaderFromHlsl(nint device,
        string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint = "main",
        bool enableDebug = false)
        => CompileGraphicsShaderFromHlsl(device,
            SDL.GPUShaderFormat.SPIRV,
            name,
            stage,
            source,
            entryPoint,
            enableDebug);

    static nint CompileGraphicsShaderFromDxbc(nint device,
        string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint,
        bool enableDebug)
    {
        byte[] dxbc = null;
        SpirVShaderMetadata metadata = null;

        Parallel.Invoke(
            () => dxbc = DxbcShaderCompiler.CompileFromHlsl(name, stage, source, entryPoint, enableDebug),
            () => metadata = SpirVShaderCompiler.CompileAndReflectSpirVFromHlsl(name,
                stage,
                source,
                entryPoint,
                enableDebug).Metadata);

        return createGraphicsShader(device,
            name,
            stage,
            SDL.GPUShaderFormat.DXBC,
            dxbc,
            entryPoint,
            metadata.Resources);
    }

    public static nint CompileGraphicsShaderFromSpirV(nint device,
        string name,
        CompiledShaderStage stage,
        byte[] spirV,
        string entryPoint = "main")
    {
        var metadata = SpirVShaderCompiler.Reflect(spirV);
        return createGraphicsShader(device,
            name,
            stage,
            SDL.GPUShaderFormat.SPIRV,
            spirV,
            entryPoint,
            metadata.Resources);
    }

    static unsafe nint createGraphicsShader(nint device,
        string name,
        CompiledShaderStage stage,
        SDL.GPUShaderFormat shaderFormat,
        byte[] bytecodeBytes,
        string entryPoint,
        ShaderResourceMetadata resources)
    {
        var entryPointPointer = Marshal.StringToHGlobalAnsi(entryPoint);
        try
        {
            fixed (byte* bytecode = bytecodeBytes)
            {
                var createInfo = new SDL.GPUShaderCreateInfo
                {
                    CodeSize = (nuint)bytecodeBytes.Length,
                    Code = (nint)bytecode,
                    Entrypoint = entryPointPointer,
                    Format = shaderFormat,
                    Stage = toSdlStage(stage),
                    NumSamplers = resources.Samplers,
                    NumStorageTextures = resources.StorageTextures,
                    NumStorageBuffers = resources.StorageBuffers,
                    NumUniformBuffers = resources.UniformBuffers
                };

                var shader = SDL.CreateGPUShader(device, in createInfo);
                if (shader == nint.Zero)
                    throw new InvalidOperationException($"Unable to create SDL GPU shader {name}: {SDL.GetError()}");

                return shader;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(entryPointPointer);
        }
    }

    static bool hasFormat(SDL.GPUShaderFormat formats, SDL.GPUShaderFormat format)
        => (formats & format) == format;

    static SDL.GPUShaderStage toSdlStage(CompiledShaderStage stage)
        => stage switch
        {
            CompiledShaderStage.Vertex => SDL.GPUShaderStage.Vertex,
            CompiledShaderStage.Fragment => SDL.GPUShaderStage.Fragment,
            CompiledShaderStage.Compute => throw new NotSupportedException("SDL graphics shaders do not support compute stages"),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };
}