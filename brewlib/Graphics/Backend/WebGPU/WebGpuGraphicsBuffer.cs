namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.CompilerServices;
using Ahjo.Wgpu;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

public sealed class WebGpuGraphicsBuffer : IGraphicsBuffer
{
    readonly WebGpuGraphicsBackend backend;
    bool disposed;

    public WebGpuGraphicsBuffer(WebGpuGraphicsBackend backend, GraphicsBufferDescription description)
    {
        this.backend = backend;
        Description = description;
        if (description.SizeInBytes > 0)
            Allocate(description.SizeInBytes);
    }

    internal WebGpuGraphicsBackend OwnerBackend => backend;
    internal bool IsDisposed => disposed;
    internal WgpuBuffer BufferHandle { get; private set; }
    internal int BindingOffset => 0;

    public GraphicsBufferDescription Description { get; }
    public int SizeInBytes { get; private set; }

    public void Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);
        if (sizeInBytes > backend.MaxBufferSize)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, $"Buffer '{Description.Name}' exceeds maxBufferSize {backend.MaxBufferSize}");

        replaceBuffer(sizeInBytes);
    }

    public void SetData<T>(scoped ReadOnlySpan<T> data) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var sizeInBytes = checked(data.Length * Unsafe.SizeOf<T>());
        if (sizeInBytes == 0) return;

        if (Description.Usage is GraphicsBufferUsage.Static)
        {
            if (BufferHandle.IsNull || SizeInBytes < sizeInBytes)
                replaceBuffer(sizeInBytes);
        }
        else
        {
            // Correctness-first: every dynamic/stream update receives a fresh native buffer.
            // This removes cross-frame overwrite races and stale buffer ID reuse entirely.
            replaceBuffer(sizeInBytes);
        }

        backend.QueueWriteBuffer(BufferHandle, 0, data);
    }

    public void Invalidate() { }

    public void Dispose()
    {
        if (disposed) return;
        releaseBuffer();
        disposed = true;
    }

    void replaceBuffer(int sizeInBytes)
    {
        releaseBuffer();
        SizeInBytes = 0;
        if (sizeInBytes == 0) return;

        BufferDescriptor descriptor = new()
        {
            Usage = toUsageFlags(Description.Target),
            Size = (ulong)sizeInBytes
        };

        BufferHandle = backend.DeviceHandle.CreateBuffer(in descriptor);
        if (BufferHandle.IsNull)
            throw new InvalidOperationException($"Unable to create WebGPU buffer '{Description.Name}' ({sizeInBytes} bytes)");

        SizeInBytes = sizeInBytes;
    }

    void releaseBuffer()
    {
        if (BufferHandle.IsNull) return;
        backend.DeferredReleases.Retire(BufferHandle);
        BufferHandle = default;
    }

    static BufferUsage toUsageFlags(GraphicsBufferTarget target)
        => target switch
        {
            GraphicsBufferTarget.Vertex => BufferUsage.Vertex | BufferUsage.CopyDst,
            GraphicsBufferTarget.Index => BufferUsage.Index | BufferUsage.CopyDst,
            GraphicsBufferTarget.Uniform => BufferUsage.Uniform | BufferUsage.CopyDst,
            GraphicsBufferTarget.Storage => BufferUsage.Storage | BufferUsage.CopyDst,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
}

public sealed class WebGpuGraphicsBufferFactory(WebGpuGraphicsBackend backend) : IGraphicsBufferFactory
{
    public IGraphicsBuffer CreateBuffer(GraphicsBufferDescription description)
        => new WebGpuGraphicsBuffer(backend, description);
}
