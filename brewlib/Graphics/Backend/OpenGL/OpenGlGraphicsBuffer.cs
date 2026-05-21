namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

public sealed class OpenGlGraphicsBuffer : IGraphicsBuffer
{
    readonly BufferTargetARB target;
    readonly BufferUsageARB usage;

    uint bufferId;
    bool disposed;

    public OpenGlGraphicsBuffer(GraphicsBufferDescription description)
    {
        Description = description;
        target = toOpenGlTarget(description.Target);
        usage = toOpenGlUsage(description.Usage);
        bufferId = OpenGlApi.GL.GenBuffer();

        if (description.SizeInBytes > 0) Allocate(description.SizeInBytes);
    }

    public GraphicsResourceHandle NativeHandle => new("OpenGL", (nint)bufferId);
    public GraphicsBufferDescription Description { get; }
    public int SizeInBytes { get; private set; }

    internal uint BufferId => bufferId;
    internal BufferTargetARB Target => target;

    public void Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);

        Bind();
        OpenGlApi.AllocateBuffer(target, sizeInBytes, usage);
        SizeInBytes = sizeInBytes;
    }

    public void SetData<T>(scoped ReadOnlySpan<T> data) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var sizeInBytes = data.Length * Unsafe.SizeOf<T>();
        Bind();

        if (data.IsEmpty) OpenGlApi.AllocateBuffer(target, sizeInBytes, usage);
        else OpenGlApi.GL.BufferData(target, data, usage);

        SizeInBytes = sizeInBytes;
    }

    public void Invalidate()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (DrawState.CanInvalidate) OpenGlApi.GL.InvalidateBufferData(bufferId);
    }

    internal void Bind() => OpenGlApi.GL.BindBuffer(target, bufferId);

    public void Dispose()
    {
        if (disposed) return;

        OpenGlApi.GL.DeleteBuffer(bufferId);
        bufferId = 0;
        disposed = true;
    }

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
