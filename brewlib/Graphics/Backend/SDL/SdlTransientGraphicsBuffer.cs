namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Collections.Generic;

public sealed class SdlTransientGraphicsBufferFactory(SdlGraphicsBackend backend) : ITransientGraphicsBufferFactory
{
    public ITransientGraphicsBuffer CreateBuffer(GraphicsBufferDescription description, int capacityInBytes)
        => new SdlTransientGraphicsBuffer(backend, description, capacityInBytes);
}

sealed class SdlTransientGraphicsBuffer : ITransientGraphicsBuffer
{
    readonly SdlGraphicsBackend backend;
    readonly GraphicsBufferDescription description;
    readonly List<Page> pages = [];
    readonly Dictionary<SdlGraphicsBuffer, Page> pagesByBuffer = [];

    Page activePage, mappedPage;
    bool disposed;
    int mappedOffset, mappedSize;
    SdlGraphicsBackend.FrameTransferUpload mappedUpload;

    public SdlTransientGraphicsBuffer(SdlGraphicsBackend backend,
        GraphicsBufferDescription description,
        int capacityInBytes)
    {
        if (capacityInBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityInBytes), capacityInBytes, null);

        this.backend = backend;
        this.description = description;
        activePage = createPage(capacityInBytes);
    }

    public IGraphicsBuffer Buffer => activePage.Buffer;
    public int CapacityInBytes => activePage.CapacityInBytes;

    public TransientBufferAllocation Allocate(int sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);
        if (mappedPage is not null)
            throw new InvalidOperationException($"{nameof(SdlTransientGraphicsBuffer)} already has a mapped range");

        var page = tryAllocate(sizeInBytes, out var offset) ??
            createPage(getPageCapacity(sizeInBytes));

        if (!page.TryAllocate(backend, backend.FrameSerial, sizeInBytes, out offset))
            throw new InvalidOperationException(
                $"Unable to allocate {sizeInBytes} bytes from SDL transient buffer {description.Name}");

        mappedUpload = backend.AllocateFrameTransfer(sizeInBytes, description.Name);
        mappedPage = page;
        mappedOffset = offset;
        mappedSize = sizeInBytes;

        return new(page.Buffer,
            offset,
            sizeInBytes,
            0,
            mappedUpload.Data,
            nint.Zero);
    }

    public void Commit(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var page = validateAllocation(in allocation, usedSizeInBytes);
        if (mappedPage is null) return;

        var activeUpload = mappedUpload;
        mappedUpload = default;
        mappedPage = null;
        mappedOffset = mappedSize = 0;

        page.FinishAllocation(allocation.Offset, allocation.PrimarySize, usedSizeInBytes);
        if (usedSizeInBytes == 0) return;

        backend.QueueBufferUpload(activeUpload.TransferBuffer,
            page.Buffer.BufferHandle,
            activeUpload.SourceOffset,
            (uint)allocation.Offset,
            (uint)usedSizeInBytes,
            allocation.Offset == 0,
            description.Name);
    }

    public void MarkSubmitted(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var page = validateAllocation(in allocation, usedSizeInBytes);
        if (usedSizeInBytes > 0)
            page.MarkSubmitted(backend.FrameSerial);
    }

    public void Dispose()
    {
        if (disposed) return;

        foreach (var page in pages)
            page.Dispose();

        pages.Clear();
        pagesByBuffer.Clear();
        mappedPage = null;
        activePage = null;
        disposed = true;
    }

    Page tryAllocate(int sizeInBytes, out int offset)
    {
        if (activePage.TryAllocate(backend, backend.FrameSerial, sizeInBytes, out offset))
            return activePage;

        for (var i = 0; i < pages.Count; ++i)
        {
            var page = pages[i];
            if (ReferenceEquals(page, activePage)) continue;
            if (!page.TryAllocate(backend, backend.FrameSerial, sizeInBytes, out offset)) continue;

            activePage = page;
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
        activePage = page;
        return page;
    }

    int getPageCapacity(int sizeInBytes)
    {
        var capacity = activePage?.CapacityInBytes ?? 1;
        while (capacity < sizeInBytes)
            capacity = checked(capacity * 2);

        return capacity;
    }

    Page validateAllocation(scoped ref readonly TransientBufferAllocation allocation, int usedSizeInBytes)
    {
        if (allocation.Buffer is not SdlGraphicsBuffer buffer)
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

    sealed class Page : IDisposable
    {
        bool disposed;
        uint frameSerial, submittedFrameSerial;
        bool hasFrame, hasSubmitted;
        int nextOffset;

        public Page(SdlGraphicsBackend backend,
            GraphicsBufferDescription description,
            int capacityInBytes)
        {
            CapacityInBytes = capacityInBytes;
            Buffer = new(backend,
                new(description.Name,
                    description.Target,
                    GraphicsBufferUsage.Stream,
                    capacityInBytes));
        }

        public int CapacityInBytes { get; }
        public SdlGraphicsBuffer Buffer { get; }

        public void Dispose()
        {
            if (disposed) return;

            Buffer.Dispose();
            disposed = true;
        }

        public bool TryAllocate(SdlGraphicsBackend backend, uint currentFrameSerial, int sizeInBytes, out int offset)
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

        public void FinishAllocation(int offset, int allocatedSize, int usedSizeInBytes)
        {
            if (nextOffset == offset + allocatedSize)
                nextOffset = offset + usedSizeInBytes;
        }

        public void MarkSubmitted(uint currentFrameSerial)
        {
            submittedFrameSerial = currentFrameSerial;
            hasSubmitted = true;
        }

        bool ensureFrame(SdlGraphicsBackend backend, uint currentFrameSerial)
        {
            if (hasFrame && frameSerial == currentFrameSerial)
                return true;

            if (hasSubmitted && !backend.IsFrameFenceSignaled(submittedFrameSerial))
                return false;

            frameSerial = currentFrameSerial;
            hasFrame = true;
            hasSubmitted = false;
            nextOffset = 0;
            return true;
        }
    }
}