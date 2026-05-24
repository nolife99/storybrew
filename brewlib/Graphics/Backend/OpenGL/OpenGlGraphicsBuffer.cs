namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

public sealed class OpenGlGraphicsBuffer : IGraphicsBuffer
{
    readonly BufferUsageARB usage;

    bool disposed;

    public OpenGlGraphicsBuffer(GraphicsBufferDescription description)
    {
        Description = description;
        Target = toOpenGlTarget(description.Target);
        usage = toOpenGlUsage(description.Usage);
        BufferId = OpenGlApi.GL.GenBuffer();

        if (description.SizeInBytes > 0) Allocate(description.SizeInBytes);
    }

    internal uint BufferId { get; private set; }

    internal BufferTargetARB Target { get; }

    public GraphicsResourceHandle NativeHandle => new("OpenGL", (nint)BufferId);
    public GraphicsBufferDescription Description { get; }
    public int SizeInBytes { get; private set; }

    public void Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);

        Bind();
        OpenGlApi.AllocateBuffer(Target, sizeInBytes, usage);
        SizeInBytes = sizeInBytes;
    }

    public void SetData<T>(scoped ReadOnlySpan<T> data) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var sizeInBytes = data.Length * Unsafe.SizeOf<T>();
        Bind();

        if (data.IsEmpty) OpenGlApi.AllocateBuffer(Target, sizeInBytes, usage);
        else OpenGlApi.GL.BufferData(Target, data, usage);

        SizeInBytes = sizeInBytes;
    }

    public void Invalidate()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (DrawState.CanInvalidate) OpenGlApi.GL.InvalidateBufferData(BufferId);
    }

    public void Dispose()
    {
        if (disposed) return;

        OpenGlApi.GL.DeleteBuffer(BufferId);
        BufferId = 0;
        disposed = true;
    }

    internal void Bind() => OpenGlApi.GL.BindBuffer(Target, BufferId);

    static BufferTargetARB toOpenGlTarget(GraphicsBufferTarget target)
        => target switch
        {
            GraphicsBufferTarget.Vertex => BufferTargetARB.ArrayBuffer,
            GraphicsBufferTarget.Index => BufferTargetARB.ElementArrayBuffer,
            GraphicsBufferTarget.Uniform => BufferTargetARB.UniformBuffer,
            GraphicsBufferTarget.Storage => BufferTargetARB.ShaderStorageBuffer,
            GraphicsBufferTarget.Indirect => BufferTargetARB.DrawIndirectBuffer,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };

    static BufferUsageARB toOpenGlUsage(GraphicsBufferUsage usage)
        => usage switch
        {
            GraphicsBufferUsage.Static => BufferUsageARB.StaticDraw,
            GraphicsBufferUsage.Dynamic => BufferUsageARB.DynamicDraw,
            GraphicsBufferUsage.Stream => BufferUsageARB.StreamDraw,
            _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null)
        };
}

public sealed class OpenGlGraphicsBufferFactory : IGraphicsBufferFactory
{
    public IGraphicsBuffer CreateBuffer(GraphicsBufferDescription description)
        => new OpenGlGraphicsBuffer(description);
}