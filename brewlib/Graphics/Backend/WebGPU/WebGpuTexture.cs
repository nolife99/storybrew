namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Textures;
using WgpuTexture = Ahjo.Wgpu.Texture;

sealed class WebGpuTexture : Texture2dRegion, IWritableTexture, ITextureSamplerIdentity, IWebGpuTexture
{
    readonly WebGpuDeviceContext deviceContext;
    readonly WebGpuSamplerCache.Entry sampler;
    int disposedFlag;
    bool mipsGenerated;
    TextureView view;

    WgpuTexture wgpuTexture;

    public WebGpuTexture(WebGpuDeviceContext deviceContext,
        IGraphicsBackend backend,
        WebGpuSamplerCache.Entry sampler,
        WgpuTexture wgpuTexture,
        TextureView view,
        int width,
        int height,
        TextureOptions options) : base(null, new(0, 0, width, height))
    {
        this.deviceContext = deviceContext;
        Backend = backend;
        this.sampler = sampler;
        this.wgpuTexture = wgpuTexture;
        this.view = view;
        Options = options;
    }

    public WgpuTexture WgpuTexture => wgpuTexture;
    public TextureOptions Options { get; }

    public GraphicsResourceHandle SamplerIdentity => sampler.Identity;

    public event Action<IWebGpuTexture> Disposing;
    public TextureView View => view;
    public WebGpuSamplerCache.Entry SamplerEntry => sampler;

    public IGraphicsBackend Backend { get; }

    // Uploads at or below this size are cheap enough to push straight through Queue.WriteTexture and flush
    // immediately; larger standalone uploads are streamed through the bounded stager instead so wgpu never
    // allocates a big transient staging buffer. In-frame uploads always use WriteTexture (flushed at EndFrame).
    internal const long StageThresholdBytes = 1 << 20;

    bool IsInFrame => Backend is WebGpuBackend { IsFrameActive: true };

    bool ShouldStage(long bytes) => !IsInFrame && bytes > StageThresholdBytes;

    void FlushIfStandalone()
    {
        if (!IsInFrame)
            deviceContext.FlushQueuedWrites();
    }

    public void Update(Color color, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        var pixel = color.ToPixel<Rgba32>();
        const int stackPixelLimit = 2048;
        var chunkWidth = Math.Min(width, stackPixelLimit);
        Span<Rgba32> chunk = stackalloc Rgba32[chunkWidth];
        chunk.Fill(pixel);
        var chunkBytes = MemoryMarshal.AsBytes(chunk);

        for (var row = 0; row < height; ++row)
        {
            var remaining = width;
            var destX = x;
            while (remaining > 0)
            {
                var writeWidth = Math.Min(remaining, stackPixelLimit);
                WriteRegion(chunkBytes[..(writeWidth * 4)], destX, y + row, writeWidth, 1);
                destX += writeWidth;
                remaining -= writeWidth;
            }
        }

        InvalidateMipsIfFull(x, y, width, height);
        FlushIfStandalone();
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        if (bitmap is null) return;

        UpdateFromImage(bitmap, x, y);
    }

    public void Update(scoped ReadOnlySpan<byte> data, int width, int height, int x, int y, int bytesPerRow)
    {
        if (width <= 0 || height <= 0) return;

        var packedBytesPerRow = checked(width * 4);
        if (bytesPerRow < packedBytesPerRow)
            throw new ArgumentOutOfRangeException(nameof(bytesPerRow), "bytesPerRow must be at least width * 4 for Rgba8");

        if (data.Length < (long)bytesPerRow * height)
            throw new ArgumentException("Source data is shorter than bytesPerRow * height", nameof(data));

        if (ShouldStage((long)width * height * 4))
        {
            deviceContext.TextureStager.UploadRaw(this, data, width, height, x, y, bytesPerRow);
            InvalidateMipsIfFull(x, y, width, height);
            return;
        }

        for (var row = 0; row < height; ++row)
            WriteRegion(data.Slice(row * bytesPerRow, packedBytesPerRow), x, y + row, width, 1);

        InvalidateMipsIfFull(x, y, width, height);
        FlushIfStandalone();
    }

    public void GenerateMipsIfNeeded(CommandEncoder encoder)
    {
        if (mipsGenerated) return;
        if (wgpuTexture.MipLevelCount <= 1) return;

        deviceContext.MipmapGenerator.Generate(encoder, wgpuTexture);
        mipsGenerated = true;
    }

    public void UpdateFromImage(Image<Rgba32> bitmap, int destX, int destY)
    {
        if (bitmap is null) return;

        var w = Math.Min(Width - destX, bitmap.Width);
        var h = Math.Min(Height - destY, bitmap.Height);
        if (w <= 0 || h <= 0) return;

        if (ShouldStage((long)w * h * 4))
        {
            deviceContext.TextureStager.UploadImage(this, bitmap, destX, destY);
            InvalidateMipsIfFull(destX, destY, w, h);
            return;
        }

        var src = bitmap.Frames.RootFrame.PixelBuffer;
        for (var row = 0; row < h; ++row)
        {
            var rowSpan = MemoryMarshal.AsBytes(src.DangerousGetRowSpan(row)[..w]);
            WriteRegion(rowSpan, destX, destY + row, w, 1);
        }

        InvalidateMipsIfFull(destX, destY, w, h);
        FlushIfStandalone();
    }

    void WriteRegion(scoped ReadOnlySpan<byte> tightlyPackedRgba, int x, int y, int width, int height)
    {
        var extent = new WGPUExtent3D
        {
            width = (uint)width,
            height = (uint)height,
            depthOrArrayLayers = 1
        };

        var origin = new WGPUOrigin3D
        {
            x = (uint)x,
            y = (uint)y,
            z = 0
        };

        deviceContext.Queue.WriteTexture(wgpuTexture,
            tightlyPackedRgba,
            (uint)(width * 4),
            (uint)height,
            in extent,
            0,
            origin);
    }

    void InvalidateMipsIfFull(int x, int y, int width, int height)
    {
        if (wgpuTexture.MipLevelCount > 1 && x == 0 && y == 0 && width == Width && height == Height)
            mipsGenerated = false;
    }

    protected override void Dispose(bool disposingManaged)
    {
        if (Interlocked.Exchange(ref disposedFlag, 1) != 0) return;

        try
        {
            Disposing?.Invoke(this);
        }
        catch (Exception ex)
        {
            SDL.LogError(LogCategory.Render, $"WebGpuTexture.Disposing handler threw: {ex.Message}");
        }

        Disposing = null;

        if (!view.IsNull)
        {
            view.Dispose();
            view = default;
        }

        if (!wgpuTexture.IsNull)
        {
            wgpuTexture.Dispose();
            wgpuTexture = default;
        }

        base.Dispose(disposingManaged);
    }
}

interface IWebGpuTexture
{
    TextureView View { get; }
    WebGpuSamplerCache.Entry SamplerEntry { get; }
    GraphicsResourceHandle SamplerIdentity { get; }
    event Action<IWebGpuTexture> Disposing;
}

/// <summary>
///     A logical sub-region over a physical <see cref="WebGpuTexture"/> whose GPU dimensions were rounded up for
///     block-compression alignment (BC formats require multiple-of-4 sizes). It reports the original logical size and
///     disposes the backing texture along with it. The region's BindableTexture is the physical texture, so binding
///     resolves to the real GPU resource and UVs normalize against the physical size — sampling covers only the real
///     pixels and excludes the edge-replicated padding.
/// </summary>
sealed class WebGpuTextureRegion : Texture2dRegion
{
    readonly WebGpuTexture backing;

    public WebGpuTextureRegion(WebGpuTexture backing, int width, int height)
        : base(backing, new(0, 0, width, height))
        => this.backing = backing;

    protected override void Dispose(bool disposing)
    {
        if (disposing) backing.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
///     A region whose backing texture stores only the opaque content sub-rect of a larger logical image (the
///     transparent margin was trimmed off before upload to save VRAM). It reports the <i>original</i> logical size so
///     sprite positioning is unchanged, exposes the content's offset/size via <see cref="ContentBounds"/>, and maps
///     UVs against the backing texture's (block-padded) physical size — so it also absorbs any BC block-padding. The
///     quad renderer offsets the draw by the content origin so the stored pixels land exactly where the full image's
///     pixels would have. Not a <see cref="Texture2dRegion"/> because its size/UVs aren't derived from a single bounds.
/// </summary>
sealed class WebGpuTrimmedRegion : ITrimmedTextureRegion
{
    readonly WebGpuTexture backing;

    public WebGpuTrimmedRegion(WebGpuTexture backing, int originalWidth, int originalHeight, Rectangle contentBounds)
    {
        this.backing = backing;
        Size = new Size(originalWidth, originalHeight);
        ContentBounds = contentBounds;

        // Content sits at (0,0) of the backing texture; normalize against the backing's physical (padded) size so the
        // BC padding columns/rows are never sampled.
        UvOrigin = Vector2.Zero;
        UvRatio = Vector2.One / new Vector2(backing.Size.Width, backing.Size.Height);
    }

    public ITexture Texture => backing;
    public Rectangle Bounds => new(0, 0, Size.Width, Size.Height);
    public Rectangle ContentBounds { get; }
    public Vector2 UvOrigin { get; }
    public Vector2 UvRatio { get; }
    public Size Size { get; }
    public int Width => Size.Width;
    public int Height => Size.Height;

    public void Dispose() => backing.Dispose();
}