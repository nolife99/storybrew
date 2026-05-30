namespace BrewLib.Graphics.Backend.WebGPU;

using System;
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

        if (description.Width == bitmap.Width
            && description.Height == bitmap.Height
            && textureFactory.TryPlanBc(bitmap, textureOptions, out var plan))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // No encode and no compressed buffer here: the plan (opacity/bounds scan) is computed on this worker
            // thread, and the bitmap is carried to Upload, which streams the encode into the GPU staging belt.
            return ValueTask.FromResult<PreparedTextureUpload>(
                new BcStreamPreparedUpload(description, bitmap, plan));
        }

        return ValueTask.FromResult<PreparedTextureUpload>(new ImagePreparedUpload(description, bitmap));
    }

    public override ITextureRegion Upload(PreparedTextureUpload upload)
    {
        if (upload is null) return null;
        if (disposed) throw new ObjectDisposedException(nameof(WebGpuAsyncTextureUploader));

        try
        {
            // Block-compressed: create the texture and stream-encode the planned sub-rect into it band by band.
            // This is where the CPU bc7f encode runs (on the device thread, like the old compute path).
            if (upload is BcStreamPreparedUpload bcUpload)
                return textureFactory.LoadBcStreamed(bcUpload.Bitmap, bcUpload.Plan, bcUpload.Options);

            if (upload is not ImagePreparedUpload prepared)
                throw new ArgumentException("Upload was not produced by this uploader", nameof(upload));

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
    
    sealed class BcStreamPreparedUpload : PreparedTextureUpload, IPreparedTextureUploadOwnsBitmap
    {
        Image bitmap;

        public BcStreamPreparedUpload(TextureUploadDescription description, Image bitmap, BcPlan plan) : base(description)
        {
            this.bitmap = bitmap;
            Plan = plan;
        }

        public Image Bitmap => bitmap;
        public BcPlan Plan { get; }

        public override void Dispose()
        {
            var local = Interlocked.Exchange(ref bitmap, null);
            local?.Dispose();
        }
    }
}
