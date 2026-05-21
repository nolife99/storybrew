namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Util;

public sealed class WebGpuTransientGraphicsBufferFactory(WebGpuGraphicsBackend backend) : ITransientGraphicsBufferFactory
{
    public ITransientGraphicsBuffer CreateBuffer(GraphicsBufferDescription description, int capacityInBytes)
        => new WebGpuTransientGraphicsBuffer(backend, description, capacityInBytes);
}

sealed class WebGpuTransientGraphicsBuffer : ITransientGraphicsBuffer
{
    readonly WebGpuGraphicsBackend backend;
    readonly GraphicsBufferDescription description;
    readonly List<Page> pages = [];
    readonly Dictionary<WebGpuGraphicsBuffer, Page> pagesByBuffer = [];
    readonly int pageCapacityInBytes;

    Page activePage;
    Page mappedPage;
    Page committedPage;
    bool disposed;
    bool registeredForFlush;
    int mappedOffset, mappedSize, mappedAllocatedSize;
    int committedOffset, committedSize;

    public WebGpuTransientGraphicsBuffer(WebGpuGraphicsBackend backend,
        GraphicsBufferDescription description,
        int capacityInBytes)
    {
        if (capacityInBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityInBytes), capacityInBytes, null);

        this.backend = backend;
        this.description = description;
        pageCapacityInBytes = int.Min(capacityInBytes, backend.MaxBufferSize);
        activePage = createPage(pageCapacityInBytes);
    }

    public IGraphicsBuffer Buffer => activePage.Buffer;
    public int CapacityInBytes => pageCapacityInBytes;

    public TransientBufferAllocation Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (mappedPage is not null)
            throw new InvalidOperationException($"{nameof(WebGpuTransientGraphicsBuffer)} already has a mapped range");
        if (sizeInBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);
        var allocatedSize = alignCopySize(sizeInBytes);
        if (allocatedSize > pageCapacityInBytes)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes),
                sizeInBytes,
                $"Transient WebGPU allocation exceeds page size {pageCapacityInBytes}.");

        var page = tryAllocate(allocatedSize, out var offset);
        if (page is null)
        {
            page = createPage(pageCapacityInBytes);
            if (!page.TryAllocate(backend, backend.FrameSerial, allocatedSize, out offset))
                throw new InvalidOperationException(
                    $"Unable to allocate {sizeInBytes} bytes from a {pageCapacityInBytes} byte WebGPU transient page");
        }

        activePage = page;
        mappedPage = page;
        mappedOffset = offset;
        mappedSize = sizeInBytes;
        mappedAllocatedSize = allocatedSize;

        return new(page.Buffer,
            offset,
            sizeInBytes,
            0,
            page.ArenaPointer.AsPointer() + offset,
            nint.Zero);
    }

    public void Commit(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (mappedPage is null) return;
        if (!ReferenceEquals(allocation.Buffer, mappedPage.Buffer) ||
            allocation.Offset != mappedOffset ||
            allocation.PrimarySize != mappedSize ||
            allocation.SecondarySize != 0)
            throw new InvalidOperationException("Transient buffer allocation is not the active mapped range");
        if (usedSizeInBytes < 0 || usedSizeInBytes > mappedSize)
            throw new ArgumentOutOfRangeException(nameof(usedSizeInBytes), usedSizeInBytes, null);

        var page = mappedPage;
        var offset = mappedOffset;
        var allocatedSize = mappedAllocatedSize;
        mappedPage = null;
        mappedOffset = mappedSize = mappedAllocatedSize = 0;
        committedPage = page;
        committedOffset = offset;
        committedSize = usedSizeInBytes;

        var usedCopySize = alignCopySize(usedSizeInBytes);
        page.FinishAllocation(offset, allocatedSize, usedCopySize);

        if (usedCopySize == 0) return;

        page.MarkDirty(offset, usedCopySize);
        if (!registeredForFlush)
        {
            backend.RegisterTransientUpload(this);
            registeredForFlush = true;
        }
    }

    public void MarkSubmitted(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (usedSizeInBytes <= 0) return;
        if (usedSizeInBytes > allocation.TotalSize)
            throw new ArgumentOutOfRangeException(nameof(usedSizeInBytes), usedSizeInBytes, null);

        var page = tryGetCommittedPage(in allocation, usedSizeInBytes) ??
            getPageForAllocation(in allocation, usedSizeInBytes);
        page.MarkSubmitted(backend.FrameSerial);
    }

    public void Dispose()
    {
        if (disposed) return;

        foreach (var page in pages)
            page.Dispose();

        pages.Clear();
        pagesByBuffer.Clear();
        activePage = null;
        mappedPage = null;
        committedPage = null;
        disposed = true;
    }

    internal void FlushPendingUploads()
    {
        registeredForFlush = false;
        if (disposed) return;

        foreach (var page in pages)
            page.Flush(backend);
    }

    Page tryAllocate(int sizeInBytes, out int offset)
    {
        if (activePage.TryAllocate(backend, backend.FrameSerial, sizeInBytes, out offset))
            return activePage;

        for (var i = 0; i < pages.Count; ++i)
        {
            var page = pages[i];
            if (ReferenceEquals(page, activePage)) continue;
            if (page.TryAllocate(backend, backend.FrameSerial, sizeInBytes, out offset))
                return page;
        }

        offset = 0;
        return null;
    }

    Page createPage(int capacityInBytes)
    {
        var page = new Page(backend, description, capacityInBytes);
        pages.Add(page);
        pagesByBuffer.Add(page.Buffer, page);
        return page;
    }

    Page tryGetCommittedPage(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        var page = committedPage;
        if (page is null ||
            !ReferenceEquals(allocation.Buffer, page.Buffer) ||
            allocation.Offset != committedOffset ||
            usedSizeInBytes > committedSize)
            return null;

        committedPage = null;
        return page;
    }

    Page getPageForAllocation(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        if (allocation.Buffer is not WebGpuGraphicsBuffer buffer)
            throw new InvalidOperationException("Transient buffer allocation belongs to another backend");

        if (!pagesByBuffer.TryGetValue(buffer, out var page))
            throw new InvalidOperationException("Transient buffer allocation belongs to another buffer");

        if (usedSizeInBytes < 0 || usedSizeInBytes > allocation.TotalSize)
            throw new ArgumentOutOfRangeException(nameof(usedSizeInBytes), usedSizeInBytes, null);

        if (allocation.SecondarySize != 0 ||
            allocation.Offset < 0 ||
            allocation.Offset + allocation.PrimarySize > page.CapacityInBytes)
            throw new InvalidOperationException("Transient buffer allocation is invalid");

        if (mappedPage is not null &&
            (!ReferenceEquals(page, mappedPage) ||
                allocation.Offset != mappedOffset ||
                allocation.PrimarySize != mappedSize ||
                allocation.SecondarySize != 0))
            throw new InvalidOperationException("Transient buffer allocation is not the active mapped range");

        return page;
    }

    static int alignCopySize(int value)
        => (value + 3) & ~3;

    sealed class Page : IDisposable
    {
        bool disposed;
        int nextOffset;
        int dirtyStart = int.MaxValue, dirtyEnd;
        uint frameSerial, submittedFrameSerial;
        bool hasFrame, hasSubmitted;

        public Page(WebGpuGraphicsBackend backend,
            GraphicsBufferDescription description,
            int capacityInBytes)
        {
            CapacityInBytes = capacityInBytes;
            Buffer = new(backend,
                new(description.Name,
                    description.Target,
                    GraphicsBufferUsage.Stream,
                    capacityInBytes));

            Arena = GC.AllocateUninitializedArray<byte>(capacityInBytes, pinned: true);
        }

        public int CapacityInBytes { get; }
        public WebGpuGraphicsBuffer Buffer { get; }
        public byte[] Arena { get; private set; }
        public ref byte ArenaPointer => ref MemoryMarshal.GetArrayDataReference(Arena);

        public bool TryAllocate(WebGpuGraphicsBackend backend, uint currentFrameSerial, int sizeInBytes, out int offset)
        {
            offset = 0;
            if (!ensureFrame(backend, currentFrameSerial))
                return false;
            if (CapacityInBytes - nextOffset < sizeInBytes)
                return false;

            offset = nextOffset;
            nextOffset += sizeInBytes;
            return true;
        }

        public void FinishAllocation(int offset, int allocatedSize, int usedCopySize)
        {
            if (nextOffset == offset + allocatedSize)
                nextOffset = offset + usedCopySize;
        }

        public void MarkDirty(int offset, int sizeInBytes)
        {
            if (offset < dirtyStart) dirtyStart = offset;
            var end = offset + sizeInBytes;
            if (end > dirtyEnd) dirtyEnd = end;
        }

        public void MarkSubmitted(uint currentFrameSerial)
        {
            submittedFrameSerial = currentFrameSerial;
            hasSubmitted = true;
        }

        public unsafe void Flush(WebGpuGraphicsBackend backend)
        {
            if (dirtyStart >= dirtyEnd) return;

            backend.Api.QueueWriteBuffer(backend.QueueHandle,
                Buffer.BufferHandle,
                (ulong)dirtyStart,
                in Unsafe.AddByteOffset(ref ArenaPointer, dirtyStart),
                (nuint)(dirtyEnd - dirtyStart));

            dirtyStart = int.MaxValue;
            dirtyEnd = 0;
        }

        public void Dispose()
        {
            if (disposed) return;

            Arena = null;

            Buffer.Dispose();
            disposed = true;
        }

        bool ensureFrame(WebGpuGraphicsBackend backend, uint currentFrameSerial)
        {
            if (hasFrame && frameSerial == currentFrameSerial)
                return true;

            if (hasSubmitted && !backend.IsFrameFenceSignaled(submittedFrameSerial))
                return false;

            frameSerial = currentFrameSerial;
            hasFrame = true;
            hasSubmitted = false;
            nextOffset = 0;
            dirtyStart = int.MaxValue;
            dirtyEnd = 0;
            return true;
        }
    }
}
