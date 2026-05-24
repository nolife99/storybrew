namespace BrewLib.Graphics.Backend;

using System;

public interface IGraphicsBuffer : IDisposable
{
    GraphicsResourceHandle NativeHandle { get; }
    GraphicsBufferDescription Description { get; }
    int SizeInBytes { get; }

    void Allocate(int sizeInBytes);
    void SetData<T>(scoped ReadOnlySpan<T> data) where T : unmanaged;
    void Invalidate();
}

public interface IGraphicsBufferFactory
{
    IGraphicsBuffer CreateBuffer(GraphicsBufferDescription description);
}

public interface ITransientGraphicsBuffer : IDisposable
{
    IGraphicsBuffer Buffer { get; }
    int CapacityInBytes { get; }

    TransientBufferAllocation Allocate(int sizeInBytes);
    void Commit(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes);
    void MarkSubmitted(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes);
}

public interface ITransientGraphicsBufferFactory
{
    ITransientGraphicsBuffer CreateBuffer(GraphicsBufferDescription description, int capacityInBytes);
}

public readonly struct TransientBufferAllocation(
    IGraphicsBuffer buffer,
    int offset,
    int primarySize,
    int secondarySize,
    nint data,
    nint secondaryData)
{
    public IGraphicsBuffer Buffer { get; } = buffer;
    public int Offset { get; } = offset;
    public int PrimarySize { get; } = primarySize;
    public int SecondarySize { get; } = secondarySize;
    public nint Data { get; } = data;
    public nint SecondaryData { get; } = secondaryData;

    public int TotalSize => PrimarySize + SecondarySize;
    public bool IsSplit => SecondarySize > 0;
}

public interface IGpuUploadFence : IDisposable
{
    bool IsSignaled { get; }
    bool CanWait { get; }

    void Wait();
}

public readonly record struct GraphicsBufferDescription(
    string Name,
    GraphicsBufferTarget Target,
    GraphicsBufferUsage Usage,
    int SizeInBytes = 0);

public enum GraphicsBufferTarget
{
    Vertex,
    Index,
    Uniform,
    Storage,
    Indirect
}

public enum GraphicsBufferUsage
{
    Static,
    Dynamic,
    Stream
}