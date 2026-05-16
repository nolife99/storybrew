namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Shaders;
using SDL3;

public sealed class SdlShaderCrossContext : IDisposable
{
    static readonly object SyncRoot = new();
    static int referenceCount;

    bool disposed;

    public SdlShaderCrossContext()
    {
        lock (SyncRoot)
        {
            if (referenceCount == 0 && !ShaderCross.Init())
                throw new InvalidOperationException($"Unable to initialize SDL_shadercross: {SDL.GetError()}");

            ++referenceCount;
        }
    }

    public static SDL.GPUShaderFormat SupportedHlslShaderFormats => ShaderCross.GetHLSLShaderFormats();
    public static SDL.GPUShaderFormat SupportedSpirVShaderFormats => ShaderCross.GetSPIRVShaderFormats();

    public void Dispose()
    {
        if (disposed) return;

        lock (SyncRoot)
        {
            --referenceCount;
            if (referenceCount == 0) ShaderCross.Quit();
        }

        disposed = true;
    }
}

public static class SdlShaderCross
{
    public static byte[] CompileSpirVFromHlsl(string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint = "main",
        bool enableDebug = false)
    {
        using var info = new ShaderCross.HLSLInfo
        {
            Source = source,
            Entrypoint = entryPoint,
            ShaderStage = toShaderCrossStage(stage),
            EnableDebug = enableDebug,
            Name = name
        };

        var bytecodePointer = ShaderCross.CompileSPIRVFromHLSL(in info, out var bytecodeSize);
        if (bytecodePointer == nint.Zero)
            throw new InvalidOperationException($"Unable to compile HLSL shader {name} to SPIR-V: {SDL.GetError()}");

        try
        {
            var bytecode = new byte[checked((int)bytecodeSize)];
            Marshal.Copy(bytecodePointer, bytecode, 0, bytecode.Length);
            return bytecode;
        }
        finally
        {
            SDL.Free(bytecodePointer);
        }
    }

    public static nint CompileGraphicsShaderFromHlsl(nint device,
        string name,
        CompiledShaderStage stage,
        string source,
        string entryPoint = "main",
        bool enableDebug = false)
    {
        var spirV = CompileSpirVFromHlsl(name, stage, source, entryPoint, enableDebug);
        return CompileGraphicsShaderFromSpirV(device, name, stage, spirV, entryPoint, enableDebug);
    }

    public static nint CompileGraphicsShaderFromSpirV(nint device,
        string name,
        CompiledShaderStage stage,
        byte[] spirV,
        string entryPoint = "main",
        bool enableDebug = false)
    {
        var handle = GCHandle.Alloc(spirV, GCHandleType.Pinned);
        try
        {
            var bytecodePointer = handle.AddrOfPinnedObject();
            var metadataPointer = ShaderCross.ReflectGraphicsSPIRV(bytecodePointer, (nuint)spirV.Length, 0);
            if (metadataPointer == nint.Zero)
                throw new InvalidOperationException($"Unable to reflect SPIR-V shader {name}: {SDL.GetError()}");

            try
            {
                var metadata = Marshal.PtrToStructure<ShaderCross.GraphicsShaderMetadata>(metadataPointer);
                using var info = new ShaderCross.SPIRVInfo
                {
                    ByteCode = bytecodePointer,
                    ByteCodeSize = (nuint)spirV.Length,
                    Entrypoint = entryPoint,
                    ShaderStage = toShaderCrossStage(stage),
                    Name = name
                };

                var shader = ShaderCross.CompileGraphicsShaderFromSPIRV(device, in info, in metadata, 0);
                if (shader == nint.Zero)
                    throw new InvalidOperationException($"Unable to create SDL GPU shader {name}: {SDL.GetError()}");

                return shader;
            }
            finally
            {
                SDL.Free(metadataPointer);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    static ShaderCross.ShaderStage toShaderCrossStage(CompiledShaderStage stage)
        => stage switch
        {
            CompiledShaderStage.Vertex => ShaderCross.ShaderStage.Vertex,
            CompiledShaderStage.Fragment => ShaderCross.ShaderStage.Fragment,
            CompiledShaderStage.Compute => ShaderCross.ShaderStage.Compute,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };
}
