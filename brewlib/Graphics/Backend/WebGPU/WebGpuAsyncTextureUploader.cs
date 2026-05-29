namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Threading;
using System.Threading.Tasks;
using Textures;
using Image = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

sealed class WebGpuAsyncTextureUploader : AsyncTextureUploaderBase
{
    readonly WebGpuBackend backend;
    readonly WebGpuDeviceContext deviceContext;
    readonly WebGpuTextureFactory textureFactory;

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
        return ValueTask.FromResult<PreparedTextureUpload>(new ImagePreparedUpload(description, bitmap));
    }

    public override ITextureRegion Upload(PreparedTextureUpload upload)
    {
        if (upload is null) return null;

        if (upload is not ImagePreparedUpload prepared)
            throw new ArgumentException("Upload was not produced by this uploader", nameof(upload));

        try
        {
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
            prepared.Dispose();
        }
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
}