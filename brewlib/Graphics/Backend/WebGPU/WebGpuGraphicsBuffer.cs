namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Ahjo.Wgpu;
using SixLabors.ImageSharp.Memory;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

public sealed class WebGpuGraphicsBuffer : IGraphicsBuffer
{
    readonly WebGpuGraphicsBackend backend;
    bool disposed, registeredForUpload;
    IMemoryOwner<byte> pendingUploadData;
    int pendingUploadStart = int.MaxValue, pendingUploadEnd;
    uint streamFrameSerial;
    int streamOffset;

    public WebGpuGraphicsBuffer(WebGpuGraphicsBackend backend, GraphicsBufferDescription description)
    {
        this.backend = backend;
        Description = description;

        if (description.SizeInBytes > 0)
            Allocate(description.SizeInBytes);
    }

    public int BindingOffset { get; private set; }
    public WgpuBuffer BufferHandle { get; private set; }

    public GraphicsBufferDescription Description { get; }
    public int SizeInBytes { get; private set; }
    public GraphicsResourceHandle NativeHandle => new(backend.Name, BufferHandle.NativeHandle());

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

        BufferHandle = backend.DeviceHandle.CreateBuffer(in descriptor);
        if (BufferHandle.IsNull)
            throw new InvalidOperationException($"Unable to create WebGPU buffer '{Description.Name}' ({sizeInBytes} bytes)");

        SizeInBytes = sizeInBytes;
    }

    public void SetData<T>(scoped ReadOnlySpan<T> data) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var sizeInBytes = checked(data.Length * Unsafe.SizeOf<T>());
        if (sizeInBytes == 0) return;

        var uploadOffset = reserveUploadRegion(sizeInBytes);
        var requiredSize = checked(uploadOffset + sizeInBytes);
        if (BufferHandle.IsNull || SizeInBytes < requiredSize)
            Allocate(getBufferAllocationSize(requiredSize));

        BindingOffset = uploadOffset;
        if (Description.Usage is GraphicsBufferUsage.Stream && backend.HasActiveFrame)
            QueueFrameUpload(data, uploadOffset, sizeInBytes);
        else
            backend.QueueHandle.WriteBuffer(BufferHandle, (ulong)uploadOffset, data);
    }

    public void Invalidate() { }

    public void Dispose()
    {
        if (disposed) return;

        releaseBuffer();
        releasePendingUploadData();
        disposed = true;
    }

    internal WebGpuBufferUpload DetachPendingUpload()
    {
        registeredForUpload = false;
        if (disposed || pendingUploadStart >= pendingUploadEnd || pendingUploadData is null)
            return default;

        var upload = new WebGpuBufferUpload(BufferHandle,
            pendingUploadData,
            pendingUploadStart,
            pendingUploadEnd - pendingUploadStart);

        pendingUploadData = null;
        pendingUploadStart = int.MaxValue;
        pendingUploadEnd = 0;
        return upload;
    }

    void QueueFrameUpload<T>(scoped ReadOnlySpan<T> data, int uploadOffset, int sizeInBytes) where T : unmanaged
    {
        EnsurePendingUploadCapacity(uploadOffset + sizeInBytes);
        MemoryMarshal.AsBytes(data).CopyTo(pendingUploadData.Memory.Span.Slice(uploadOffset, sizeInBytes));
        if (uploadOffset < pendingUploadStart) pendingUploadStart = uploadOffset;
        var uploadEnd = uploadOffset + sizeInBytes;
        if (uploadEnd > pendingUploadEnd) pendingUploadEnd = uploadEnd;

        if (registeredForUpload) return;

        registeredForUpload = true;
        backend.RegisterBufferUpload(this);
    }

    void EnsurePendingUploadCapacity(int requiredSize)
    {
        if (pendingUploadData?.Memory is { Length: var length } && length >= requiredSize)
            return;

        var capacity = int.Max(256, pendingUploadData?.Memory.Length ?? 0);
        while (capacity < requiredSize)
            capacity = checked(capacity * 2);

        var replacement = MemoryAllocator.Default.Allocate<byte>(capacity);
        if (pendingUploadData is not null)
        {
            if (pendingUploadEnd > 0)
                pendingUploadData.Memory[..pendingUploadEnd].CopyTo(replacement.Memory);

            pendingUploadData.Dispose();
        }

        pendingUploadData = replacement;
    }

    void releaseBuffer()
    {
        if (BufferHandle.IsNull) return;

        backend.DeferredReleases.Retire(BufferHandle);
        BufferHandle = default;
    }

    void releasePendingUploadData()
    {
        if (pendingUploadData is null) return;

        pendingUploadData.Dispose();
        pendingUploadData = null;
        pendingUploadStart = int.MaxValue;
        pendingUploadEnd = 0;
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