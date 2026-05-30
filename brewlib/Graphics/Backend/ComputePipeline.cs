namespace BrewLib.Graphics.Backend;

using System;
using Shaders;
using Textures;

public interface IComputePipelineFactory
{
    IComputePipeline CreateComputePipeline(ComputePipelineDescription description);
}

public interface IComputePipeline : IDisposable
{
    ComputePipelineDescription Description { get; }
    IComputeResourceSet CreateResourceSet();

    void Dispatch(in DispatchCommand command, IComputeResourceSet resources);
}

public interface IComputeResourceSet : IDisposable
{
    void SetStorageBuffer(int binding, IGraphicsBuffer buffer);
    void SetUniformBuffer(int binding, IGraphicsBuffer buffer);
}

public sealed class ComputePipelineDescription(
    string name,
    ShaderProgramSource shaderSource,
    string entryPoint,
    ComputePipelineLayout layout)
{
    public string Name { get; } = name;
    public ShaderProgramSource ShaderSource { get; } = shaderSource;
    public string EntryPoint { get; } = entryPoint;
    public ComputePipelineLayout Layout { get; } = layout;
}

public sealed class ComputePipelineLayout(params ComputeBindingLayout[] bindings)
{
    public ReadOnlySpan<ComputeBindingLayout> Bindings => bindings;
}

public readonly record struct ComputeBindingLayout(int Binding, ComputeBindingKind Kind);

public enum ComputeBindingKind
{
    StorageBufferReadOnly,
    StorageBufferReadWrite,
    UniformBuffer
}

public readonly record struct DispatchCommand(int GroupCountX, int GroupCountY = 1, int GroupCountZ = 1);

public readonly record struct BufferTextureCopy(
    IGraphicsBuffer Source,
    ITexture Destination,
    int BufferOffsetBytes,
    int BufferRowBytes,
    int RowCount,
    int Width,
    int Height,
    int MipLevel = 0);
