namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Ahjo.Wgpu.Native;
using Ahjo.Wgpu.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

delegate void BlockBandEncoder(int blockRowStart, int blockRowCount, Span<byte> bandDst);

sealed class WebGpuTextureStager : IDisposable
{
    const ulong ChunkSize = 4 * 1024 * 1024;

    public static bool UsePagePool = true;

    readonly WebGpuDeviceContext deviceContext;
    StagingBelt belt;
    byte[] packScratch;
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

        EnsureBelt();

        var destination = texture.WgpuTexture;
        var format = destination.Format;
        var blocksWide = (physWidth + 3) / 4;
        var blockRows = (physHeight + 3) / 4;
        var tightRowBytes = blocksWide * blockBytes;
        var paddedRowBytes = (int)AlignUp((ulong)tightRowBytes, 256);
        var rowsPerBand = Math.Max(1, (int)(ChunkSize / (ulong)paddedRowBytes));

        var blockRow = 0;
        while (blockRow < blockRows)
        {
            var bandRows = Math.Min(rowsPerBand, blockRows - blockRow);

            var band = blocks.Slice(blockRow * tightRowBytes, bandRows * tightRowBytes);
            var extent = new WGPUExtent3D { width = (uint)physWidth, height = (uint)(bandRows * 4), depthOrArrayLayers = 1 };
            var origin = new WGPUOrigin3D { x = 0, y = (uint)(blockRow * 4), z = 0 };

            SubmitBeltWrite(destination, in extent, format, band, origin);

            blockRow += bandRows;
        }
    }

    public void UploadCompressedStreaming(WebGpuTexture texture,
        int physWidth,
        int physHeight,
        int blockBytes,
        BlockBandEncoder encodeBand)
    {
        if (physWidth <= 0 || physHeight <= 0) return;

        var destination = texture.WgpuTexture;
        var format = destination.Format;
        var blocksWide = (physWidth + 3) / 4;
        var blockRows = (physHeight + 3) / 4;
        var tightRowBytes = blocksWide * blockBytes;
        var paddedRowBytes = (int)AlignUp((ulong)tightRowBytes, 256);
        var rowsPerBand = Math.Max(1, (int)(ChunkSize / (ulong)paddedRowBytes));

        var useBelt = UsePagePool;
        if (useBelt) EnsureBelt();
        EnsureScratch(Math.Min(rowsPerBand, blockRows) * tightRowBytes);

        var blockRow = 0;
        while (blockRow < blockRows)
        {
            var bandRows = Math.Min(rowsPerBand, blockRows - blockRow);
            var band = packScratch.AsSpan(0, bandRows * tightRowBytes);

            encodeBand(blockRow, bandRows, band);

            var extent = new WGPUExtent3D { width = (uint)physWidth, height = (uint)(bandRows * 4), depthOrArrayLayers = 1 };
            var origin = new WGPUOrigin3D { x = 0, y = (uint)(blockRow * 4), z = 0 };

            if (useBelt)
                SubmitBeltWrite(destination, in extent, format, band, origin);
            else
                deviceContext.Queue.WriteTexture(destination, band, (uint)tightRowBytes, (uint)bandRows, in extent, 0, origin);

            blockRow += bandRows;
        }
    }

    void UploadRegion(WebGpuTexture texture, int destX, int destY, int width, int height, scoped in RowSource source)
        => UploadRegionRaw(texture.WgpuTexture, destX, destY, width, height, in source);

    void UploadRegionRaw(Ahjo.Wgpu.Texture destination, int destX, int destY, int width, int height, scoped in RowSource source)
    {
        EnsureBelt();

        var format = destination.Format;
        var tightRowBytes = width * 4;
        var paddedRowBytes = (int)AlignUp((ulong)tightRowBytes, 256);
        var rowsPerBand = Math.Max(1, (int)(ChunkSize / (ulong)paddedRowBytes));
        EnsureScratch(rowsPerBand * tightRowBytes);

        var row = 0;
        while (row < height)
        {
            var bandRows = Math.Min(rowsPerBand, height - row);
            var band = packScratch.AsSpan(0, bandRows * tightRowBytes);

            for (var r = 0; r < bandRows; ++r)
                source.CopyRow(row + r, band.Slice(r * tightRowBytes, tightRowBytes));

            var extent = new WGPUExtent3D { width = (uint)width, height = (uint)bandRows, depthOrArrayLayers = 1 };
            var origin = new WGPUOrigin3D { x = (uint)destX, y = (uint)(destY + row), z = 0 };

            SubmitBeltWrite(destination, in extent, format, band, origin);

            row += bandRows;
        }
    }

    void SubmitBeltWrite(Ahjo.Wgpu.Texture destination,
        scoped in WGPUExtent3D extent,
        WGPUTextureFormat format,
        scoped ReadOnlySpan<byte> tightData,
        WGPUOrigin3D origin)
    {
        using (var encoder = deviceContext.Device.CreateCommandEncoder())
        {
            belt.WriteTexture(encoder, destination, in extent, format, tightData, 0, origin, WGPUTextureAspect.All);
            belt.Finish();

            using var cmd = encoder.Finish();
            deviceContext.Queue.Submit(cmd);
        }

        belt.Recall();
        WaitForBeltRecall();
    }

    void EnsureBelt()
    {
        if (belt is not null) return;
        if (disposed) throw new ObjectDisposedException(nameof(WebGpuTextureStager));

        belt = new(deviceContext.Device, ChunkSize);
    }

    void EnsureScratch(int needed)
    {
        if (packScratch is null || packScratch.Length < needed)
            packScratch = GC.AllocateUninitializedArray<byte>(needed);
    }

    void WaitForBeltRecall()
    {
        while (belt.InFlightChunks > 0)
        {
            deviceContext.Device.ProcessEvents();
            belt.Poll();
            if (belt.InFlightChunks > 0)
                Thread.Yield();
        }
    }

    static ulong AlignUp(ulong value, ulong alignment)
        => (value + alignment - 1) & ~(alignment - 1);

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

        belt?.Dispose();
        belt = null;
    }
    
    readonly ref struct RowSource
    {
        readonly ReadOnlySpan<byte> contiguous;
        readonly Buffer2D<Rgba32> rows;
        readonly int sourceStride;
        readonly int width;
        readonly byte kind;

        RowSource(ReadOnlySpan<byte> contiguous, Buffer2D<Rgba32> rows, int sourceStride, int width, byte kind)
        {
            this.contiguous = contiguous;
            this.rows = rows;
            this.sourceStride = sourceStride;
            this.width = width;
            this.kind = kind;
        }

        public static RowSource Contiguous(ReadOnlySpan<byte> data, int sourceStride, int width)
            => new(data, null, sourceStride, width, 0);

        public static RowSource Rows(Buffer2D<Rgba32> buffer, int width)
            => new(default, buffer, 0, width, 1);

        public void CopyRow(int sourceRow, scoped Span<byte> destination)
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
}
