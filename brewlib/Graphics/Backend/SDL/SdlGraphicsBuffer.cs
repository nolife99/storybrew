namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Backend;
using SDL3;

public sealed class SdlGraphicsBuffer : IGraphicsBuffer
{
    readonly SdlGraphicsBackend backend;

    nint bufferHandle;
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

    public void Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);

        if (bufferHandle != nint.Zero) SDL.ReleaseGPUBuffer(backend.DeviceHandle, bufferHandle);

        if (sizeInBytes == 0)
        {
            SizeInBytes = 0;
            bufferHandle = nint.Zero;
            return;
        }

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

        var sizeInBytes = data.Length * Unsafe.SizeOf<T>();
        if (bufferHandle == nint.Zero || SizeInBytes != sizeInBytes) Allocate(sizeInBytes);
        if (sizeInBytes == 0) return;

        var bytes = MemoryMarshal.AsBytes(data).ToArray();
        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)sizeInBytes
        };

        var transferBuffer = SDL.CreateGPUTransferBuffer(backend.DeviceHandle, in transferCreateInfo);

        if (transferBuffer == nint.Zero)
            throw new InvalidOperationException(
                $"Unable to create SDL GPU transfer buffer for {Description.Name}: {SDL.GetError()}");

        try
        {
            var mapped = SDL.MapGPUTransferBuffer(backend.DeviceHandle, transferBuffer, false);
            if (mapped == nint.Zero)
                throw new InvalidOperationException(
                    $"Unable to map SDL GPU transfer buffer for {Description.Name}: {SDL.GetError()}");

            Marshal.Copy(bytes, 0, mapped, bytes.Length);
            SDL.UnmapGPUTransferBuffer(backend.DeviceHandle, transferBuffer);

            var commandBuffer = SDL.AcquireGPUCommandBuffer(backend.DeviceHandle);
            var copyPass = SDL.BeginGPUCopyPass(commandBuffer);

            var source = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer };
            var destination = new SDL.GPUBufferRegion
            {
                Buffer = bufferHandle,
                Size = (uint)sizeInBytes
            };

            SDL.UploadToGPUBuffer(copyPass, in source, in destination, true);
            SDL.EndGPUCopyPass(copyPass);

            if (!SDL.SubmitGPUCommandBuffer(commandBuffer))
                throw new InvalidOperationException(
                    $"Unable to submit SDL GPU upload for {Description.Name}: {SDL.GetError()}");
        }
        finally
        {
            SDL.ReleaseGPUTransferBuffer(backend.DeviceHandle, transferBuffer);
        }
    }

    public void Invalidate()
    {
    }

    public void Dispose()
    {
        if (disposed) return;

        if (bufferHandle != nint.Zero) SDL.ReleaseGPUBuffer(backend.DeviceHandle, bufferHandle);
        bufferHandle = nint.Zero;
        disposed = true;
    }

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
