namespace BrewLib.Graphics.Backend;

using System;

public interface IGraphicsBuffer : IDisposable
{
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
}

public enum GraphicsBufferUsage
{
    Static,
    Dynamic,
    Stream
}
