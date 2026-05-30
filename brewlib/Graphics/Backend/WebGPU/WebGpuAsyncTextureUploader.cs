namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Util;
using Textures;
using Image = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

sealed class WebGpuAsyncTextureUploader : AsyncTextureUploaderBase
{
    readonly WebGpuBackend backend;
    readonly WebGpuDeviceContext deviceContext;
    readonly WebGpuTextureFactory textureFactory;

    bool disposed;

    public WebGpuAsyncTextureUploader(WebGpuBackend backend,
        WebGpuTextureFactory textureFactory,
        WebGpuDeviceContext deviceContext,
        Func<int> maxTextureSizeProvider) : base(maxTextureSizeProvider)
    {
        this.backend = backend;
        this.textureFactory = textureFactory;
        this.deviceContext = deviceContext;
    }

    protected override ValueTask<PreparedTextureUpload> PrepareAsync(string source,
        Image bitmap,
        TextureOptions textureOptions,
        CancellationToken cancellationToken)
    {
        var description = CreateDescription(source, bitmap, textureOptions);

        // Compress on THIS worker thread when block compression applies (and no max-size clamping is needed, so the
        // plan's content rect is in the bitmap's own pixel space). The main-thread Upload step then only creates the
        // texture and writes the small block buffer, so the app never freezes while compressing. TryPlanBc picks BC1
        // for opaque frames / BC7 otherwise and trims transparent frames to their opaque bounds; anything it rejects
        // (small/mipped/fully-transparent) takes the RGBA upload path.
        if (description.Width == bitmap.Width
            && description.Height == bitmap.Height
            && bitmap.DangerousTryGetSinglePixelMemory(out var pixels)
            && textureFactory.TryPlanBc(bitmap, textureOptions, out var plan))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // GPU mode: don't encode on the worker — hand the (owned) bitmap to the device-thread Upload, which runs
            // the compute dispatch. The dispatch only records commands, so the main thread doesn't stall on it.
            if (WebGpuTextureFactory.UseGpuCompression)
                return ValueTask.FromResult<PreparedTextureUpload>(
                    new BcGpuPreparedUpload(description, bitmap, plan));

            var rgba = MemoryMarshal.AsBytes(pixels.Span);
            var srcStride = bitmap.Width * 4;
            var offset = plan.ContentY * srcStride + plan.ContentX * 4;

            var encodedSize = Bc7Encoder.EncodedSize(plan.ContentW, plan.ContentH, plan.Format);
            var blocks = ArrayPool<byte>.Shared.Rent(encodedSize);

            Bc7Encoder.Encode(rgba.Slice(offset),
                plan.ContentW,
                plan.ContentH,
                srcStride,
                blocks.AsSpan(0, encodedSize),
                plan.Format,
                Bc7Encoder.Quality.Basic,
                plan.HasAlpha);

            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult<PreparedTextureUpload>(
                new Bc7PreparedUpload(description, blocks, encodedSize, plan, bitmap.Width, bitmap.Height));
        }

        return ValueTask.FromResult<PreparedTextureUpload>(new ImagePreparedUpload(description, bitmap));
    }

    public override ITextureRegion Upload(PreparedTextureUpload upload)
    {
        if (upload is null) return null;
        if (disposed) throw new ObjectDisposedException(nameof(WebGpuAsyncTextureUploader));

        try
        {
            // GPU mode: the encode was deferred to here (device thread). Run the compute dispatch from the bitmap's
            // pixels; fall back to an uncompressed upload if the pixels aren't contiguous or BC is unsupported.
            if (upload is BcGpuPreparedUpload gpu)
            {
                if (gpu.Bitmap.DangerousTryGetSinglePixelMemory(out var gpuPixels))
                {
                    var region = textureFactory.EncodeBcOnGpu(MemoryMarshal.AsBytes(gpuPixels.Span),
                        gpu.OriginalWidth, gpu.OriginalHeight, gpu.Plan, gpu.Options);
                    if (region is not null) return region;
                }

                var bcFallback = (WebGpuTexture)textureFactory.CreateEmpty(gpu.OriginalWidth, gpu.OriginalHeight, gpu.Options);
                bcFallback.UpdateFromImage(gpu.Bitmap, 0, 0);
                return bcFallback;
            }

            // The blocks were already encoded on the worker thread; the device thread only creates the texture and
            // writes the (small) block buffer, then wraps it (trimmed/padded region) per the plan.
            if (upload is Bc7PreparedUpload bc)
                return textureFactory.CreateBcFromBlocks(bc.Blocks, bc.Plan.ContentW, bc.Plan.ContentH, bc.Plan.Format,
                    bc.Options, bc.OriginalWidth, bc.OriginalHeight, bc.Plan.ContentX, bc.Plan.ContentY);

            if (upload is not ImagePreparedUpload prepared)
                throw new ArgumentException("Upload was not produced by this uploader", nameof(upload));

            // CreateEmpty + UpdateFromImage routes the pixel copy through the shared, bounded texture stager, so wgpu
            // never accumulates per-upload staging.
            var texture = (WebGpuTexture)textureFactory.CreateEmpty(prepared.Width, prepared.Height, prepared.Options);
            texture.UpdateFromImage(prepared.Bitmap, 0, 0);

            if (prepared.Options.GenerateMipmaps && texture.WgpuTexture.MipLevelCount > 1)
            {
                using var encoder = deviceContext.Device.CreateCommandEncoder();
                texture.GenerateMipsIfNeeded(encoder);
                using var cmd = encoder.Finish();
                deviceContext.Queue.Submit(cmd);
            }

            return texture;
        }
        finally
        {
            upload.Dispose();
        }
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
    }

    sealed class ImagePreparedUpload : PreparedTextureUpload, IPreparedTextureUploadOwnsBitmap
    {
        Image bitmap;

        public ImagePreparedUpload(TextureUploadDescription description, Image bitmap) : base(description)
            => this.bitmap = bitmap;

        public Image Bitmap => bitmap;

        public override void Dispose()
        {
            var local = Interlocked.Exchange(ref bitmap, null);
            local?.Dispose();
        }
    }

    // GPU-compression mode: carries the source bitmap (owned) to the device-thread Upload, where the compute encoder
    // runs the dispatch. Like ImagePreparedUpload it owns and disposes the bitmap, since the encode is deferred.
    sealed class BcGpuPreparedUpload : PreparedTextureUpload, IPreparedTextureUploadOwnsBitmap
    {
        Image bitmap;

        public BcGpuPreparedUpload(TextureUploadDescription description, Image bitmap, WebGpuTextureFactory.BcPlan plan) : base(description)
        {
            this.bitmap = bitmap;
            Plan = plan;
            OriginalWidth = bitmap.Width;
            OriginalHeight = bitmap.Height;
        }

        public Image Bitmap => bitmap;
        public WebGpuTextureFactory.BcPlan Plan { get; }
        public int OriginalWidth { get; }
        public int OriginalHeight { get; }

        public override void Dispose()
        {
            var local = Interlocked.Exchange(ref bitmap, null);
            local?.Dispose();
        }
    }

    // Holds block-compressed data encoded on the worker thread, plus the plan (format + content rect) and the original
    // image size needed to wrap it on the device thread. Does NOT own the source bitmap (the encode already consumed
    // it), so the base class disposes the bitmap right after PrepareAsync returns.
    sealed class Bc7PreparedUpload : PreparedTextureUpload
    {
        byte[] blocks;

        public Bc7PreparedUpload(TextureUploadDescription description,
            byte[] blocks,
            int blockLength,
            WebGpuTextureFactory.BcPlan plan,
            int originalWidth,
            int originalHeight) : base(description)
        {
            this.blocks = blocks;
            BlockLength = blockLength;
            Plan = plan;
            OriginalWidth = originalWidth;
            OriginalHeight = originalHeight;
        }

        public int BlockLength { get; }
        public WebGpuTextureFactory.BcPlan Plan { get; }
        public int OriginalWidth { get; }
        public int OriginalHeight { get; }
        public ReadOnlySpan<byte> Blocks => blocks.AsSpan(0, BlockLength);

        public override void Dispose()
        {
            var local = Interlocked.Exchange(ref blocks, null);
            if (local is not null) ArrayPool<byte>.Shared.Return(local);
        }
    }
}
