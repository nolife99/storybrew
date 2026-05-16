namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Backend;
using osuTK.Graphics.OpenGL;

public sealed class OpenGlGraphicsBuffer : IGraphicsBuffer
{
    readonly BufferTarget target;
    readonly BufferUsageHint usage;

    int bufferId;
    bool disposed;

    public OpenGlGraphicsBuffer(GraphicsBufferDescription description)
    {
        Description = description;
        target = toOpenGlTarget(description.Target);
        usage = toOpenGlUsage(description.Usage);
        bufferId = GL.GenBuffer();

        if (description.SizeInBytes > 0) Allocate(description.SizeInBytes);
    }

    public GraphicsResourceHandle NativeHandle => new("OpenGL", bufferId);
    public GraphicsBufferDescription Description { get; }
    public int SizeInBytes { get; private set; }

    internal int BufferId => bufferId;
    internal BufferTarget Target => target;

    public void Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);

        Bind();
        GL.BufferData(target, sizeInBytes, IntPtr.Zero, usage);
        SizeInBytes = sizeInBytes;
    }

    public void SetData<T>(scoped ReadOnlySpan<T> data) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var sizeInBytes = data.Length * Unsafe.SizeOf<T>();
        Bind();

        if (data.IsEmpty) GL.BufferData(target, sizeInBytes, IntPtr.Zero, usage);
        else
            GL.BufferData(target,
                sizeInBytes,
                ref MemoryMarshal.GetReference(data),
                usage);

        SizeInBytes = sizeInBytes;
    }

    public void Invalidate()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (DrawState.CanInvalidate) GL.InvalidateBufferData(bufferId);
    }

    internal void Bind() => GL.BindBuffer(target, bufferId);

    public void Dispose()
    {
        if (disposed) return;

        GL.DeleteBuffer(bufferId);
        bufferId = 0;
        disposed = true;
    }

    static BufferTarget toOpenGlTarget(GraphicsBufferTarget target)
        => target switch
        {
            GraphicsBufferTarget.Vertex => BufferTarget.ArrayBuffer,
            GraphicsBufferTarget.Index => BufferTarget.ElementArrayBuffer,
            GraphicsBufferTarget.Uniform => BufferTarget.UniformBuffer,
            GraphicsBufferTarget.Storage => BufferTarget.ShaderStorageBuffer,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };

    static BufferUsageHint toOpenGlUsage(GraphicsBufferUsage usage)
        => usage switch
        {
            GraphicsBufferUsage.Static => BufferUsageHint.StaticDraw,
            GraphicsBufferUsage.Dynamic => BufferUsageHint.DynamicDraw,
            GraphicsBufferUsage.Stream => BufferUsageHint.StreamDraw,
            _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, null)
        };
}

public sealed class OpenGlGraphicsBufferFactory : IGraphicsBufferFactory
{
    public IGraphicsBuffer CreateBuffer(GraphicsBufferDescription description)
        => new OpenGlGraphicsBuffer(description);
}
