namespace BrewLib.Graphics.Backend.WebGPU;

using System;
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

    public void Update(Color color, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        var pixel = color.ToPixel<Rgba32>();
        const int stackRowPixelLimit = 2048;

        if (width <= stackRowPixelLimit)
        {
            Span<Rgba32> row = stackalloc Rgba32[width];
            row.Fill(pixel);
            WriteRowRepeated(MemoryMarshal.AsBytes(row), x, y, width, height);
        }
        else
        {
            var rowArr = new Rgba32[width];
            rowArr.AsSpan().Fill(pixel);
            WriteRowRepeated(MemoryMarshal.AsBytes(rowArr.AsSpan()), x, y, width, height);
        }

        InvalidateMipsIfFull(x, y, width, height);
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

        if (bytesPerRow == packedBytesPerRow)
        {
            WriteRegion(data[..(packedBytesPerRow * height)], x, y, width, height);
        }
        else
        {
            for (var row = 0; row < height; ++row)
                WriteRegion(data.Slice(row * bytesPerRow, packedBytesPerRow), x, y + row, width, 1);
        }

        InvalidateMipsIfFull(x, y, width, height);
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

        if (w == bitmap.Width && h == bitmap.Height && bitmap.DangerousTryGetSinglePixelMemory(out var pixels))
        {
            WriteRegion(MemoryMarshal.AsBytes(pixels.Span), destX, destY, w, h);
        }
        else
        {
            var src = bitmap.Frames.RootFrame.PixelBuffer;
            for (var row = 0; row < h; ++row)
            {
                var rowSpan = MemoryMarshal.AsBytes(src.DangerousGetRowSpan(row)[..w]);
                WriteRegion(rowSpan, destX, destY + row, w, 1);
            }
        }

        InvalidateMipsIfFull(destX, destY, w, h);
    }

    void WriteRowRepeated(scoped ReadOnlySpan<byte> oneRow, int x, int y, int width, int height)
    {
        for (var row = 0; row < height; ++row)
            WriteRegion(oneRow, x, y + row, width, 1);
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