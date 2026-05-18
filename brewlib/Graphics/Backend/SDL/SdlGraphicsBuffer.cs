namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Backend;
using BrewLib.Util;
using SDL3;

public sealed class SdlGraphicsBuffer : IGraphicsBuffer
{
    readonly SdlGraphicsBackend backend;

    nint bufferHandle, transferBufferHandle;
    int transferBufferSize, streamOffset;
    uint streamFrameSerial;
    bool disposed;

    public SdlGraphicsBuffer(SdlGraphicsBackend backend, GraphicsBufferDescription description)
    {
        this.backend = backend;
        Description = description;

        if (description.SizeInBytes > 0) Allocate(description.SizeInBytes);
    }

    public GraphicsResourceHandle NativeHandle => new(backend.Name, bufferHandle);
    public GraphicsBufferDescription Description { get; }
    public int SizeInBytes { get; private set; }
    public nint BufferHandle => bufferHandle;
    public int BindingOffset { get; private set; }

    public void Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);

        if (bufferHandle != nint.Zero)
        {
            backend.ReleaseBuffer(bufferHandle);
            bufferHandle = nint.Zero;
        }

        SizeInBytes = 0;
        BindingOffset = 0;

        if (sizeInBytes == 0) return;

        var createInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = toUsageFlags(Description.Target),
            Size = (uint)sizeInBytes
        };

        bufferHandle = SDL.CreateGPUBuffer(backend.DeviceHandle, in createInfo);
        if (bufferHandle == nint.Zero)
            throw new InvalidOperationException($"Unable to create SDL GPU buffer {Description.Name}: {SDL.GetError()}");

        SizeInBytes = sizeInBytes;
    }

    public void SetData<T>(scoped ReadOnlySpan<T> data) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var sizeInBytes = checked(data.Length * Unsafe.SizeOf<T>());
        if (sizeInBytes == 0) return;

        var uploadOffset = reserveUploadRegion(sizeInBytes);
        var requiredSize = checked(uploadOffset + sizeInBytes);
        if (bufferHandle == nint.Zero || SizeInBytes < requiredSize)
            Allocate(getBufferAllocationSize(requiredSize));

        BindingOffset = uploadOffset;
        ensureTransferBuffer(sizeInBytes);

        var mapped = SDL.MapGPUTransferBuffer(backend.DeviceHandle, transferBufferHandle, true);
        if (mapped == nint.Zero)
            throw new InvalidOperationException(
                $"Unable to map SDL GPU transfer buffer for {Description.Name}: {SDL.GetError()}");

        MemoryMarshal.AsBytes(data).CopyTo(mapped.AsSpan<byte>(sizeInBytes));
        SDL.UnmapGPUTransferBuffer(backend.DeviceHandle, transferBufferHandle);

        backend.UploadBuffer(transferBufferHandle,
            bufferHandle,
            0,
            (uint)uploadOffset,
            (uint)sizeInBytes,
            Description.Usage is not GraphicsBufferUsage.Static && uploadOffset == 0,
            Description.Name);
    }

    public void Invalidate()
    {
    }

    public void Dispose()
    {
        if (disposed) return;

        backend.ReleaseBuffer(bufferHandle);
        backend.ReleaseTransferBuffer(transferBufferHandle);

        bufferHandle = nint.Zero;
        transferBufferHandle = nint.Zero;
        transferBufferSize = 0;
        SizeInBytes = 0;
        disposed = true;
    }

    void ensureTransferBuffer(int sizeInBytes)
    {
        if (transferBufferHandle != nint.Zero && transferBufferSize >= sizeInBytes) return;

        backend.ReleaseTransferBuffer(transferBufferHandle);
        transferBufferHandle = nint.Zero;

        transferBufferSize = getTransferBufferAllocationSize(sizeInBytes);
        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)transferBufferSize
        };

        transferBufferHandle = SDL.CreateGPUTransferBuffer(backend.DeviceHandle, in transferCreateInfo);
        if (transferBufferHandle == nint.Zero)
            throw new InvalidOperationException(
                $"Unable to create SDL GPU transfer buffer for {Description.Name}: {SDL.GetError()}");
    }

    int getBufferAllocationSize(int sizeInBytes)
    {
        if (Description.Usage is GraphicsBufferUsage.Static) return sizeInBytes;

        var minimum = int.Max(sizeInBytes, 256);
        var current = SizeInBytes;
        if (current <= 0) return minimum;

        while (current < minimum)
            current = checked(current * 2);

        return current;
    }

    int getTransferBufferAllocationSize(int sizeInBytes)
    {
        if (Description.Usage is GraphicsBufferUsage.Static) return sizeInBytes;

        var minimum = int.Max(sizeInBytes, 256);
        var current = transferBufferSize;
        if (current <= 0) return minimum;

        while (current < minimum)
            current = checked(current * 2);

        return current;
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

        var offset = align(streamOffset, 16);
        streamOffset = align(checked(offset + sizeInBytes), 16);
        return offset;
    }

    static int align(int value, int alignment)
        => (value + alignment - 1) & ~(alignment - 1);

    static SDL.GPUBufferUsageFlags toUsageFlags(GraphicsBufferTarget target)
        => target switch
        {
            GraphicsBufferTarget.Vertex => SDL.GPUBufferUsageFlags.Vertex,
            GraphicsBufferTarget.Index => SDL.GPUBufferUsageFlags.Index,
            GraphicsBufferTarget.Uniform => SDL.GPUBufferUsageFlags.GraphicsStorageRead,
            GraphicsBufferTarget.Storage => SDL.GPUBufferUsageFlags.GraphicsStorageRead | SDL.GPUBufferUsageFlags.ComputeStorageRead,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
}

public sealed class SdlGraphicsBufferFactory(SdlGraphicsBackend backend) : IGraphicsBufferFactory
{
    public IGraphicsBuffer CreateBuffer(GraphicsBufferDescription description)
        => new SdlGraphicsBuffer(backend, description);
}
