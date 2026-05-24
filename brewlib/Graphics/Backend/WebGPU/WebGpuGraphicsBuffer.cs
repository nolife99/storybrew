namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;
using WgpuBufferUsage = Silk.NET.WebGPU.BufferUsage;

public unsafe sealed class WebGpuGraphicsBuffer : IGraphicsBuffer
{
    readonly WebGpuGraphicsBackend backend;

    bool disposed;
    uint streamFrameSerial;
    int streamOffset;

    public WebGpuGraphicsBuffer(WebGpuGraphicsBackend backend, GraphicsBufferDescription description)
    {
        this.backend = backend;
        Description = description;

        if (description.SizeInBytes > 0) Allocate(description.SizeInBytes);
    }

    public WgpuBuffer* BufferHandle { get; private set; }

    public int BindingOffset { get; private set; }

    public GraphicsResourceHandle NativeHandle => new(backend.Name, (nint)BufferHandle);
    public GraphicsBufferDescription Description { get; }
    public int SizeInBytes { get; private set; }

    public void Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);
        if (sizeInBytes > backend.MaxBufferSize)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes),
                sizeInBytes,
                $"WebGPU buffer '{Description.Name}' exceeds device maxBufferSize {backend.MaxBufferSize}");

        releaseBuffer();

        SizeInBytes = 0;
        BindingOffset = 0;
        streamOffset = 0;

        if (sizeInBytes == 0) return;

        BufferDescriptor descriptor = new()
        {
            Usage = toUsageFlags(Description.Target),
            Size = (ulong)sizeInBytes
        };

        BufferHandle = backend.Api.DeviceCreateBuffer(backend.DeviceHandle, in descriptor);
        if (BufferHandle is null)
            throw new InvalidOperationException($"Unable to create WebGPU buffer {Description.Name}");

        SizeInBytes = sizeInBytes;
    }

    public void SetData<T>(scoped ReadOnlySpan<T> data) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var sizeInBytes = checked(data.Length * Unsafe.SizeOf<T>());
        if (sizeInBytes == 0) return;

        var uploadOffset = reserveUploadRegion(sizeInBytes);
        var requiredSize = checked(uploadOffset + sizeInBytes);
        if (BufferHandle is null || SizeInBytes < requiredSize)
            Allocate(getBufferAllocationSize(requiredSize));

        BindingOffset = uploadOffset;

        var bytes = MemoryMarshal.AsBytes(data);
        fixed (byte* source = bytes)
            backend.Api.QueueWriteBuffer(backend.QueueHandle,
                BufferHandle,
                (ulong)uploadOffset,
                source,
                (nuint)sizeInBytes);
    }

    public void Invalidate() { }

    public void Dispose()
    {
        if (disposed) return;

        releaseBuffer();
        disposed = true;
    }

    void releaseBuffer()
    {
        if (BufferHandle is null) return;

        backend.RetireBuffer(BufferHandle);
        BufferHandle = null;
    }

    int reserveUploadRegion(int sizeInBytes)
    {
        if (Description.Usage is GraphicsBufferUsage.Static)
            return 0;

        var frameSerial = backend.FrameSerial;
        if (streamFrameSerial != frameSerial)
        {
            streamFrameSerial = frameSerial;
            streamOffset = 0;
        }

        var offset = align(streamOffset, 4);
        streamOffset = align(checked(offset + sizeInBytes), 4);
        return offset;
    }

    int getBufferAllocationSize(int sizeInBytes)
    {
        if (sizeInBytes > backend.MaxBufferSize)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes),
                sizeInBytes,
                $"WebGPU buffer '{Description.Name}' exceeds device maxBufferSize {backend.MaxBufferSize}");

        if (Description.Usage is GraphicsBufferUsage.Static) return sizeInBytes;

        var minimum = int.Max(sizeInBytes, 256);
        var current = SizeInBytes;
        if (current <= 0) return minimum;

        while (current < minimum)
            current = (int)Math.Min((long)current * 2, backend.MaxBufferSize);

        return current;
    }

    static int align(int value, int alignment)
        => value + alignment - 1 & ~(alignment - 1);

    static WgpuBufferUsage toUsageFlags(GraphicsBufferTarget target)
        => target switch
        {
            GraphicsBufferTarget.Vertex => WgpuBufferUsage.Vertex | WgpuBufferUsage.CopyDst,
            GraphicsBufferTarget.Index => WgpuBufferUsage.Index | WgpuBufferUsage.CopyDst,
            GraphicsBufferTarget.Uniform => WgpuBufferUsage.Uniform | WgpuBufferUsage.CopyDst,
            GraphicsBufferTarget.Storage => WgpuBufferUsage.Storage | WgpuBufferUsage.CopyDst,
            GraphicsBufferTarget.Indirect => WgpuBufferUsage.Indirect | WgpuBufferUsage.CopyDst,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
}

public sealed class WebGpuGraphicsBufferFactory(WebGpuGraphicsBackend backend) : IGraphicsBufferFactory
{
    public IGraphicsBuffer CreateBuffer(GraphicsBufferDescription description)
        => new WebGpuGraphicsBuffer(backend, description);
}