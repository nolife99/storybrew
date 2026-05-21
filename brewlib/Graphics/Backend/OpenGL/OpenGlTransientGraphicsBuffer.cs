namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using Silk.NET.OpenGL;

public sealed class OpenGlTransientGraphicsBufferFactory(OpenGlGraphicsBackend backend) : ITransientGraphicsBufferFactory
{
    public ITransientGraphicsBuffer CreateBuffer(GraphicsBufferDescription description, int capacityInBytes)
        => new OpenGlTransientGraphicsBuffer(backend, description, capacityInBytes);
}

sealed class OpenGlTransientGraphicsBuffer : ITransientGraphicsBuffer
{
    const MapBufferAccessMask MapFlags =
        MapBufferAccessMask.WriteBit |
        MapBufferAccessMask.InvalidateRangeBit |
        MapBufferAccessMask.UnsynchronizedBit;

    readonly OpenGlGraphicsBackend backend;
    readonly GraphicsBufferDescription description;

    OpenGlGraphicsBuffer buffer;
    UploadRingAllocator allocator;
    nint mapped;
    int mappedOffset, mappedSize;
    bool disposed;

    public OpenGlTransientGraphicsBuffer(OpenGlGraphicsBackend backend,
        GraphicsBufferDescription description,
        int capacityInBytes)
    {
        if (capacityInBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityInBytes), capacityInBytes, null);

        this.backend = backend;
        this.description = description;
        createBuffer(capacityInBytes);
    }

    public IGraphicsBuffer Buffer => buffer;
    public int CapacityInBytes => allocator.CapacityInBytes;

    public TransientBufferAllocation Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (mapped != nint.Zero)
            throw new InvalidOperationException($"{nameof(OpenGlTransientGraphicsBuffer)} already has a mapped range");

        ensureCapacity(sizeInBytes);

        if (!allocator.TryAllocate(sizeInBytes, false, out var allocation))
            allocation = allocator.Allocate(sizeInBytes, false);

        buffer.Bind();
        mapped = OpenGlApi.MapBufferRange(buffer.Target, allocation.Offset, sizeInBytes, MapFlags);
        if (mapped == nint.Zero)
            throw new InvalidOperationException($"Unable to map OpenGL transient buffer {description.Name}: {OpenGlApi.GL.GetError()}");

        mappedOffset = allocation.Offset;
        mappedSize = sizeInBytes;
        return new(buffer, allocation.Offset, sizeInBytes, 0, mapped, nint.Zero);
    }

    public void Commit(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        validateAllocation(in allocation, usedSizeInBytes);
        if (mapped == nint.Zero) return;

        buffer.Bind();
        OpenGlApi.GL.UnmapBuffer(buffer.Target);
        mapped = nint.Zero;
        mappedOffset = mappedSize = 0;
    }

    public void MarkSubmitted(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        validateAllocation(in allocation, usedSizeInBytes);
        allocator.Protect(allocation.Offset, usedSizeInBytes, backend.CreateUploadFence());
    }

    public void Dispose()
    {
        if (disposed) return;

        if (mapped != nint.Zero)
        {
            buffer.Bind();
            OpenGlApi.GL.UnmapBuffer(buffer.Target);
            mapped = nint.Zero;
        }

        allocator.Dispose();
        buffer.Dispose();
        disposed = true;
    }

    void ensureCapacity(int sizeInBytes)
    {
        if (sizeInBytes <= CapacityInBytes) return;

        var capacity = CapacityInBytes;
        while (capacity < sizeInBytes)
            capacity = checked(capacity * 2);
        recreateBuffer(capacity);
    }

    void recreateBuffer(int capacityInBytes)
    {
        allocator.Dispose();
        buffer.Dispose();
        createBuffer(capacityInBytes);
    }

    void createBuffer(int capacityInBytes)
    {
        buffer = new(new(description.Name,
            description.Target,
            GraphicsBufferUsage.Stream,
            capacityInBytes));
        allocator = new(capacityInBytes);
    }

    void validateAllocation(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        if (allocation.Buffer != buffer)
            throw new InvalidOperationException("Transient buffer allocation belongs to another buffer");
        if (usedSizeInBytes < 0 || usedSizeInBytes > allocation.PrimarySize)
            throw new ArgumentOutOfRangeException(nameof(usedSizeInBytes), usedSizeInBytes, null);
        if (mapped != nint.Zero && (allocation.Offset != mappedOffset || allocation.PrimarySize != mappedSize))
            throw new InvalidOperationException("Transient buffer allocation is not the active mapped range");
    }
}

sealed class OpenGlUploadFence(nint fence) : IGpuUploadFence
{
    bool disposed;

    public bool IsSignaled
    {
        get
        {
            if (disposed || fence == nint.Zero) return true;

            OpenGlApi.GL.GetSync(fence, SyncParameterName.SyncStatus, sizeof(int), out _, out var status);
            return status == (int)GLEnum.Signaled;
        }
    }

    public bool CanWait => true;

    public void Wait()
    {
        if (disposed || fence == nint.Zero) return;

        var result = OpenGlApi.GL.ClientWaitSync(fence,
            SyncObjectMask.Bit,
            ulong.MaxValue);
        if (result == GLEnum.WaitFailed)
            throw new InvalidOperationException($"OpenGL upload fence wait failed: {OpenGlApi.GL.GetError()}");
    }

    public void Dispose()
    {
        if (disposed) return;

        if (fence != nint.Zero)
        {
            OpenGlApi.GL.DeleteSync(fence);
            fence = nint.Zero;
        }

        disposed = true;
    }
}
