namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Buffer2D = SixLabors.ImageSharp.Memory.Buffer2D<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using WgpuBuffer = Ahjo.Wgpu.Buffer;

/// <summary>
///     Shared, main-thread-only staging for texture uploads.
///
///     Uploads are streamed through a small, reused pool of persistently-mapped staging buffers and copied into
///     the destination texture with <c>CopyBufferToTexture</c>, submitting one command buffer per page batch. This
///     is the same mechanism the async uploader uses; routing the synchronous load/update path through it as well
///     is what keeps wgpu's <c>gpu-allocator</c> high-water mark bounded.
///
///     The reason this matters: <c>Queue.WriteTexture</c> allocates an internal staging buffer per call and keeps
///     it alive in wgpu's pending-write list until the next <c>Queue.Submit</c>. During startup there is no submit
///     until the first frame, so every texture's staging coexists and the allocator reserves (and then never
///     releases) host-visible blocks equal to the sum of all uploads. Submitting per batch here lets wgpu recycle
///     staging immediately, so the peak — and therefore the reserved/"unbound" memory — stays at the page pool size.
///
///     The page pool is allocated lazily on first use (so merely constructing the backend reserves nothing) and is
///     reused for the lifetime of the device.
/// </summary>
sealed unsafe class WebGpuTextureStager : IDisposable
{
    const ulong PageSize = 16 * 1024 * 1024;
    const int PageCount = 4;

    /// <summary>
    ///     Diagnostic switch. When false, every upload bypasses the mapped page pool and goes straight through
    ///     <c>Queue.WriteTexture</c> (wgpu's internal staging), and the 64 MiB page pool is never allocated — so the
    ///     belt contributes zero device memory. This is the pre-belt behaviour, kept behind a flag so the belt's
    ///     contribution to the device-local high-water can be A/B-measured. Leaving it false gives up the startup-load
    ///     staging bound the belt provides: with no submit during a no-frame load burst, wgpu's per-write staging
    ///     coexists and the climb can balloon back toward the original blow-up. It does not affect the post-load
    ///     "unbound" plateau, which is gpu-alloc's retained free-list, not staging.
    /// </summary>
    public static bool UsePagePool = true;

    readonly WebGpuDeviceContext deviceContext;
    UploadPage[] pages;
    bool disposed;

    public WebGpuTextureStager(WebGpuDeviceContext deviceContext)
        => this.deviceContext = deviceContext ?? throw new ArgumentNullException(nameof(deviceContext));

    public void UploadImage(WebGpuTexture texture, Image<Rgba32> bitmap, int destX, int destY)
    {
        if (bitmap is null) return;

        var width = Math.Min(texture.Width - destX, bitmap.Width);
        var height = Math.Min(texture.Height - destY, bitmap.Height);
        if (width <= 0 || height <= 0) return;

        if (!UsePagePool)
        {
            WriteImageDirect(texture, bitmap, destX, destY, width, height);
            return;
        }

        if (bitmap.DangerousTryGetSinglePixelMemory(out var pixels))
        {
            var source = RowSource.Contiguous(MemoryMarshal.AsBytes(pixels.Span), bitmap.Width * 4, width);
            UploadRegion(texture, destX, destY, width, height, in source);
        }
        else
        {
            var source = RowSource.Rows(bitmap.Frames.RootFrame.PixelBuffer, width);
            UploadRegion(texture, destX, destY, width, height, in source);
        }
    }

    public void UploadRaw(WebGpuTexture texture,
        scoped ReadOnlySpan<byte> data,
        int width,
        int height,
        int destX,
        int destY,
        int sourceBytesPerRow)
    {
        if (width <= 0 || height <= 0) return;

        var packedBytesPerRow = checked(width * 4);
        if (sourceBytesPerRow < packedBytesPerRow)
            throw new ArgumentOutOfRangeException(nameof(sourceBytesPerRow), "bytesPerRow must be at least width * 4 for Rgba8");
        if (data.Length < (long)sourceBytesPerRow * height)
            throw new ArgumentException("Source data is shorter than bytesPerRow * height", nameof(data));

        if (!UsePagePool)
        {
            WriteRawDirect(texture, data, width, height, destX, destY, sourceBytesPerRow);
            return;
        }

        var source = RowSource.Contiguous(data, sourceBytesPerRow, width);
        UploadRegion(texture, destX, destY, width, height, in source);
    }

    /// <summary>
    ///     Uploads tightly-/padded-RGBA into a raw (non-<see cref="WebGpuTexture"/>) destination through the same
    ///     bounded page pool as the pixel paths. The GPU BC encoder uses this to fill its reused scratch source
    ///     texture, so the compress source upload bypasses <c>Queue.WriteTexture</c>'s internal staging and stays inside
    ///     the page pool — matching the bounded behaviour of the regular RGBA upload path.
    /// </summary>
    public void UploadRawToTexture(Ahjo.Wgpu.Texture destination,
        scoped ReadOnlySpan<byte> data,
        int width,
        int height,
        int destX,
        int destY,
        int sourceBytesPerRow)
    {
        if (width <= 0 || height <= 0) return;

        var packedBytesPerRow = checked(width * 4);
        if (sourceBytesPerRow < packedBytesPerRow)
            throw new ArgumentOutOfRangeException(nameof(sourceBytesPerRow), "bytesPerRow must be at least width * 4 for Rgba8");
        if (data.Length < (long)sourceBytesPerRow * height)
            throw new ArgumentException("Source data is shorter than bytesPerRow * height", nameof(data));

        if (!UsePagePool)
        {
            WriteRawTextureDirect(destination, data, width, height, destX, destY, sourceBytesPerRow);
            return;
        }

        var source = RowSource.Contiguous(data, sourceBytesPerRow, width);
        UploadRegionRaw(destination, destX, destY, width, height, in source);
    }

    /// <summary>
    ///     Uploads tightly-packed BC blocks into a block-compressed texture through the same bounded page pool as the
    ///     pixel paths. Each block row is repacked to the 256-byte <c>CopyBufferToTexture</c> alignment in a staging
    ///     page and copied per page batch. Unlike <c>Queue.WriteTexture</c> — whose internal staging is not reclaimed
    ///     until the next device poll, so it piles up across a no-frame startup load burst — only the page pool is ever
    ///     resident here, which keeps the device-local high-water (and the allocator's retained/unbound blocks) bounded.
    /// </summary>
    public void UploadCompressedBlocks(WebGpuTexture texture,
        scoped ReadOnlySpan<byte> blocks,
        int physWidth,
        int physHeight,
        int blockBytes)
    {
        if (physWidth <= 0 || physHeight <= 0) return;

        if (!UsePagePool)
        {
            WriteBlocksDirect(texture, blocks, physWidth, physHeight, blockBytes);
            return;
        }

        EnsurePages();

        var blocksWide = (physWidth + 3) / 4;
        var blockRows = (physHeight + 3) / 4;
        var tightRowBytes = blocksWide * blockBytes;
        var destinationBytesPerRow = (int)AlignUp((ulong)tightRowBytes, 256);
        if ((ulong)destinationBytesPerRow > PageSize)
            throw new InvalidOperationException($"Compressed block row {destinationBytesPerRow} exceeds upload page size {PageSize}.");

        var rowsPerPage = Math.Max(1, (int)Math.Min((ulong)blockRows, PageSize / (ulong)destinationBytesPerRow));
        var wgpuTexture = texture.WgpuTexture;

        var blockRow = 0;
        while (blockRow < blockRows)
        {
            using var encoder = deviceContext.Device.CreateCommandEncoder();

            var usedPages = 0;
            for (; blockRow < blockRows && usedPages < pages.Length; ++usedPages)
            {
                var page = pages[usedPages];
                WaitForPageMapped(page);

                var rows = Math.Min(rowsPerPage, blockRows - blockRow);
                var uploadBytes = checked((ulong)destinationBytesPerRow * (ulong)rows);
                var destination = page.GetMappedSpan(uploadBytes);

                for (var r = 0; r < rows; ++r)
                    blocks.Slice((blockRow + r) * tightRowBytes, tightRowBytes)
                        .CopyTo(destination.Slice(r * destinationBytesPerRow, tightRowBytes));

                page.UnmapForSubmit();

                // Block rows map to 4 texel rows each; copy this batch into the matching texel band of the texture.
                CopyPageBlocksToTexture(encoder,
                    page.Buffer,
                    wgpuTexture,
                    (uint)destinationBytesPerRow,
                    (uint)rows,
                    (uint)physWidth,
                    (uint)(rows * 4),
                    (uint)(blockRow * 4));

                blockRow += rows;
            }

            using var cmd = encoder.Finish();
            deviceContext.Queue.Submit(cmd);

            for (var i = 0; i < usedPages; ++i)
                pages[i].BeginRemap();
        }
    }

    void UploadRegion(WebGpuTexture texture, int destX, int destY, int width, int height, scoped in RowSource source)
        => UploadRegionRaw(texture.WgpuTexture, destX, destY, width, height, in source);

    void UploadRegionRaw(Ahjo.Wgpu.Texture wgpuTexture, int destX, int destY, int width, int height, scoped in RowSource source)
    {
        EnsurePages();

        var packedBytesPerRow = width * 4;
        var destinationBytesPerRow = (int)AlignUp((ulong)packedBytesPerRow, 256);
        if ((ulong)destinationBytesPerRow > PageSize)
            throw new InvalidOperationException($"Texture row upload size {destinationBytesPerRow} exceeds upload page size {PageSize}.");

        var rowsPerPage = Math.Max(1, (int)Math.Min((ulong)height, PageSize / (ulong)destinationBytesPerRow));

        var row = 0;
        while (row < height)
        {
            using var encoder = deviceContext.Device.CreateCommandEncoder();

            var usedPages = 0;
            for (; row < height && usedPages < pages.Length; ++usedPages)
            {
                var page = pages[usedPages];
                WaitForPageMapped(page);

                var rows = Math.Min(rowsPerPage, height - row);
                var uploadBytes = checked((ulong)destinationBytesPerRow * (ulong)rows);
                var destination = page.GetMappedSpan(uploadBytes);

                for (var r = 0; r < rows; ++r)
                    source.CopyRow(row + r, destination.Slice(r * destinationBytesPerRow, packedBytesPerRow));

                page.UnmapForSubmit();

                CopyPageToTexture(encoder,
                    page.Buffer,
                    wgpuTexture,
                    (uint)destinationBytesPerRow,
                    (uint)rows,
                    (uint)width,
                    (uint)rows,
                    (uint)destX,
                    (uint)(destY + row));

                row += rows;
            }

            using var cmd = encoder.Finish();
            deviceContext.Queue.Submit(cmd);

            for (var i = 0; i < usedPages; ++i)
                pages[i].BeginRemap();
        }
    }

    void EnsurePages()
    {
        if (pages is not null) return;
        if (disposed) throw new ObjectDisposedException(nameof(WebGpuTextureStager));

        var created = new UploadPage[PageCount];
        for (var i = 0; i < created.Length; ++i)
            created[i] = new UploadPage(deviceContext.Device, PageSize);

        pages = created;
    }

    void WaitForPageMapped(UploadPage page)
    {
        while (!page.IsMapped)
        {
            deviceContext.Device.ProcessEvents();
            page.PollMap();
            if (!page.IsMapped)
                Thread.Yield();
        }
    }

    static void CopyPageToTexture(CommandEncoder encoder,
        WgpuBuffer source,
        Ahjo.Wgpu.Texture destination,
        uint bytesPerRow,
        uint rowsPerImage,
        uint width,
        uint height,
        uint x,
        uint y)
    {
        var bufferInfo = new WGPUTexelCopyBufferInfo
        {
            buffer = source.Handle,
            layout = new WGPUTexelCopyBufferLayout
            {
                offset = 0,
                bytesPerRow = bytesPerRow,
                rowsPerImage = rowsPerImage
            }
        };

        var textureInfo = new WGPUTexelCopyTextureInfo
        {
            texture = destination.Handle,
            mipLevel = 0,
            origin = new WGPUOrigin3D { x = x, y = y, z = 0 },
            aspect = WGPUTextureAspect.All
        };

        var extent = new WGPUExtent3D
        {
            width = width,
            height = height,
            depthOrArrayLayers = 1
        };

        WGPU.wgpuCommandEncoderCopyBufferToTexture(encoder.Handle, &bufferInfo, &textureInfo, &extent);
    }

    static void CopyPageBlocksToTexture(CommandEncoder encoder,
        WgpuBuffer source,
        Ahjo.Wgpu.Texture destination,
        uint bytesPerRow,
        uint blockRows,
        uint widthTexels,
        uint heightTexels,
        uint yTexels)
    {
        var bufferInfo = new WGPUTexelCopyBufferInfo
        {
            buffer = source.Handle,
            layout = new WGPUTexelCopyBufferLayout
            {
                offset = 0,
                bytesPerRow = bytesPerRow,
                rowsPerImage = blockRows
            }
        };

        var textureInfo = new WGPUTexelCopyTextureInfo
        {
            texture = destination.Handle,
            mipLevel = 0,
            origin = new WGPUOrigin3D { x = 0, y = yTexels, z = 0 },
            aspect = WGPUTextureAspect.All
        };

        var extent = new WGPUExtent3D
        {
            width = widthTexels,
            height = heightTexels,
            depthOrArrayLayers = 1
        };

        WGPU.wgpuCommandEncoderCopyBufferToTexture(encoder.Handle, &bufferInfo, &textureInfo, &extent);
    }

    static ulong AlignUp(ulong value, ulong alignment)
        => (value + alignment - 1) & ~(alignment - 1);

    // --- Direct Queue.WriteTexture bypass (UsePagePool == false) ----------------------------------------------
    // wgpu's WriteTexture lays out its own internal staging, so (unlike CopyBufferToTexture) bytesPerRow does NOT
    // need 256-byte alignment here and tight rows can be handed over as-is. FlushQueuedWrites is intentionally not
    // called per upload: this faithfully reproduces the pre-belt path, where the per-write staging is not consumed
    // until the next submit (none during a no-frame startup load).

    void WriteBlocksDirect(WebGpuTexture texture,
        scoped ReadOnlySpan<byte> blocks,
        int physWidth,
        int physHeight,
        int blockBytes)
    {
        var blocksWide = (physWidth + 3) / 4;
        var blockRows = (physHeight + 3) / 4;
        var extent = new WGPUExtent3D { width = (uint)physWidth, height = (uint)physHeight, depthOrArrayLayers = 1 };

        deviceContext.Queue.WriteTexture(texture.WgpuTexture,
            blocks,
            (uint)(blocksWide * blockBytes),
            (uint)blockRows,
            in extent);
    }

    void WriteRawDirect(WebGpuTexture texture,
        scoped ReadOnlySpan<byte> data,
        int width,
        int height,
        int destX,
        int destY,
        int sourceBytesPerRow)
        => WriteRawTextureDirect(texture.WgpuTexture, data, width, height, destX, destY, sourceBytesPerRow);

    void WriteRawTextureDirect(Ahjo.Wgpu.Texture destination,
        scoped ReadOnlySpan<byte> data,
        int width,
        int height,
        int destX,
        int destY,
        int sourceBytesPerRow)
    {
        var extent = new WGPUExtent3D { width = (uint)width, height = (uint)height, depthOrArrayLayers = 1 };
        var origin = new WGPUOrigin3D { x = (uint)destX, y = (uint)destY, z = 0 };

        deviceContext.Queue.WriteTexture(destination,
            data,
            (uint)sourceBytesPerRow,
            (uint)height,
            in extent,
            0,
            origin);
    }

    void WriteImageDirect(WebGpuTexture texture, Image<Rgba32> bitmap, int destX, int destY, int width, int height)
    {
        var extent = new WGPUExtent3D { width = (uint)width, height = (uint)height, depthOrArrayLayers = 1 };
        var origin = new WGPUOrigin3D { x = (uint)destX, y = (uint)destY, z = 0 };

        if (bitmap.DangerousTryGetSinglePixelMemory(out var pixels))
        {
            deviceContext.Queue.WriteTexture(texture.WgpuTexture,
                MemoryMarshal.AsBytes(pixels.Span),
                (uint)(bitmap.Width * 4),
                (uint)height,
                in extent,
                0,
                origin);
            return;
        }

        // Non-contiguous backing: pack the copied region into a tight temporary, then upload it in one call.
        var rowBytes = width * 4;
        using var owner = Configuration.Default.MemoryAllocator.Allocate<byte>(rowBytes * height);
        var packed = owner.Memory.Span;
        var src = bitmap.Frames.RootFrame.PixelBuffer;
        for (var row = 0; row < height; ++row)
            MemoryMarshal.AsBytes(src.DangerousGetRowSpan(row)[..width]).CopyTo(packed.Slice(row * rowBytes, rowBytes));

        deviceContext.Queue.WriteTexture(texture.WgpuTexture,
            packed,
            (uint)rowBytes,
            (uint)height,
            in extent,
            0,
            origin);
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        if (pages is null) return;

        foreach (var page in pages)
        {
            if (page is null) continue;
            WaitForPageMapped(page);
            page.Dispose();
        }

        pages = null;
    }

    /// <summary>
    ///     A pixel-row source for the page copy loop. A ref struct so it can hold the contiguous span without an
    ///     intermediate allocation; the row-segmented case keeps a reference to ImageSharp's per-row buffer instead.
    /// </summary>
    readonly ref struct RowSource
    {
        readonly ReadOnlySpan<byte> contiguous;
        readonly Buffer2D rows;
        readonly int sourceStride;
        readonly int width;
        readonly byte kind;

        RowSource(ReadOnlySpan<byte> contiguous, Buffer2D rows, int sourceStride, int width, byte kind)
        {
            this.contiguous = contiguous;
            this.rows = rows;
            this.sourceStride = sourceStride;
            this.width = width;
            this.kind = kind;
        }

        public static RowSource Contiguous(ReadOnlySpan<byte> data, int sourceStride, int width)
            => new(data, null, sourceStride, width, 0);

        public static RowSource Rows(Buffer2D buffer, int width)
            => new(default, buffer, 0, width, 1);

        public void CopyRow(int sourceRow, Span<byte> destination)
        {
            switch (kind)
            {
                case 0:
                    contiguous.Slice(sourceRow * sourceStride, width * 4).CopyTo(destination);
                    break;

                case 1:
                    MemoryMarshal.AsBytes(rows.DangerousGetRowSpan(sourceRow)[..width]).CopyTo(destination);
                    break;
            }
        }
    }

    sealed unsafe class UploadPage : IDisposable
    {
        readonly Device device;
        readonly ulong size;
        WgpuBuffer buffer;
        byte* mappedPtr;
        BufferMapRequest mapRequest;
        bool disposed;

        public UploadPage(Device device, ulong size)
        {
            this.device = device;
            this.size = size;
            buffer = device.CreateBuffer(new BufferDescriptor
            {
                Size = size,
                Usage = BufferUsage.MapWrite | BufferUsage.CopySrc,
                MappedAtCreation = true
            });

            mappedPtr = (byte*)WGPU.wgpuBufferGetMappedRange(buffer.Handle, UIntPtr.Zero, (UIntPtr)size);
            if (mappedPtr == null)
            {
                buffer.Dispose();
                throw new InvalidOperationException("wgpuBufferGetMappedRange returned null for a freshly created texture upload page.");
            }
        }

        public WgpuBuffer Buffer => buffer;
        public bool IsMapped => mappedPtr != null;

        public Span<byte> GetMappedSpan(ulong length)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UploadPage));
            if (mappedPtr == null) throw new InvalidOperationException("Texture upload page is not mapped.");
            if (length > size) throw new ArgumentOutOfRangeException(nameof(length));
            return new Span<byte>(mappedPtr, checked((int)length));
        }

        public void UnmapForSubmit()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UploadPage));
            if (mappedPtr == null) return;

            buffer.Unmap();
            mappedPtr = null;
        }

        public void BeginRemap()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UploadPage));
            if (mappedPtr != null) return;
            if (!mapRequest.Equals(default(BufferMapRequest)))
                throw new InvalidOperationException("Texture upload page is already remapping.");

            mapRequest = buffer.BeginMap(MapMode.Write, 0, (UIntPtr)size);
        }

        public void PollMap()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UploadPage));
            if (mappedPtr != null) return;
            if (mapRequest.Equals(default(BufferMapRequest))) return;
            if (!mapRequest.IsComplete) return;

            if (!mapRequest.IsSuccess)
            {
                var status = mapRequest.Status;
                mapRequest.Dispose();
                mapRequest = default;
                throw new InvalidOperationException($"Texture upload page remap failed with status {status}.");
            }

            mapRequest.Dispose();
            mapRequest = default;

            mappedPtr = (byte*)WGPU.wgpuBufferGetMappedRange(buffer.Handle, UIntPtr.Zero, (UIntPtr)size);
            if (mappedPtr == null)
                throw new InvalidOperationException("wgpuBufferGetMappedRange returned null after texture upload page remap.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            mapRequest.Dispose();
            mapRequest = default;

            if (mappedPtr != null)
            {
                buffer.Unmap();
                mappedPtr = null;
            }

            if (!buffer.IsNull)
            {
                WGPU.wgpuBufferDestroy(buffer.Handle);
                buffer.Dispose();
                buffer = default;
            }
        }
    }
}
