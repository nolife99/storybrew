namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using BrewLib.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Textures;

sealed class WebGpuTextureFactory : ITextureFactory, IDisposable
{
    readonly IGraphicsBackend backend;
    readonly WebGpuDeviceContext deviceContext;
    readonly WebGpuSamplerCache samplerCache;

    WebGpuBcComputeEncoder computeEncoder;
    bool disposed;

    public WebGpuTextureFactory(WebGpuDeviceContext deviceContext,
        WebGpuSamplerCache samplerCache,
        IGraphicsBackend backend)
    {
        this.deviceContext = deviceContext;
        this.samplerCache = samplerCache;
        this.backend = backend;
    }

    public static bool UseGpuCompression { get; set; } = true;

    WebGpuBcComputeEncoder ComputeEncoder => computeEncoder ??= new WebGpuBcComputeEncoder(deviceContext);

    public void PrewarmGpuCompression() => ComputeEncoder.BeginPrewarm();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        computeEncoder?.Dispose();
        computeEncoder = null;
    }

    public ITextureRegion Load(Image<Rgba32> bitmap, TextureOptions textureOptions = null)
    {
        if (bitmap is null) return null;

        textureOptions ??= TextureOptions.Default;

        if (bitmap.DangerousTryGetSinglePixelMemory(out var pixels)
            && TryPlanBc(bitmap, textureOptions, out var plan))
        {
            var rgba = MemoryMarshal.AsBytes(pixels.Span);
            var region = UseGpuCompression
                ? EncodeBcOnGpu(rgba, bitmap.Width, bitmap.Height, plan, textureOptions)
                : LoadBcPlanned(rgba, bitmap.Width, bitmap.Height, plan, textureOptions);
            if (region is not null) return region;
        }

        var texture = CreateInternal(bitmap.Width, bitmap.Height, textureOptions, null);

        texture.UpdateFromImage(bitmap, 0, 0);
        if (textureOptions.GenerateMipmaps && texture.WgpuTexture.MipLevelCount > 1)
        {
            using var encoder = deviceContext.Device.CreateCommandEncoder();
            texture.GenerateMipsIfNeeded(encoder);
            using var cmd = encoder.Finish();
            deviceContext.Queue.Submit(cmd);
        }

        return texture;
    }

    public static int Bc7MinArea { get; set; } = 128 * 128;

    bool ShouldCompress(Image<Rgba32> bitmap, TextureOptions options)
        => ShouldCompressBc7(bitmap.Width, bitmap.Height, options);

    internal bool ShouldCompressBc7(int width, int height, TextureOptions options)
    {
        return false;
        if (!deviceContext.HasBcCompression) return false; // device lacks the BC feature
        if (options.GenerateMipmaps) return false; // BC7 path is single-mip
        return (long)width * height >= Bc7MinArea;
    }

    public static bool TrimTransparent { get; set; } = true;

    internal readonly record struct BcPlan(Bc7Encoder.BcFormat Format, int ContentX, int ContentY, int ContentW, int ContentH, bool HasAlpha);

    internal bool TryPlanBc(Image<Rgba32> bitmap, TextureOptions options, out BcPlan plan)
    {
        plan = default;
        if (!ShouldCompressBc7(bitmap.Width, bitmap.Height, options)) return false;

        if (bitmap.IsFullyOpaque())
        {
            plan = new(Bc7Encoder.BcFormat.Bc1, 0, 0, bitmap.Width, bitmap.Height, false);
            return true;
        }

        var x = 0;
        var y = 0;
        var w = bitmap.Width;
        var h = bitmap.Height;

        if (TrimTransparent)
        {
            var bounds = BitmapHelper.FindTransparencyBounds(bitmap);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            if (bounds.Width < w || bounds.Height < h)
            {
                x = bounds.X;
                y = bounds.Y;
                w = bounds.Width;
                h = bounds.Height;
            }
        }

        plan = new(Bc7Encoder.BcFormat.Bc7, x, y, w, h, true);
        return true;
    }

    ITextureRegion LoadBcPlanned(ReadOnlySpan<byte> rgba, int originalWidth, int originalHeight, BcPlan plan, TextureOptions options)
    {
        var srcStride = originalWidth * 4;
        var offset = plan.ContentY * srcStride + plan.ContentX * 4;
        var content = rgba.Slice(offset);

        var encodedSize = Bc7Encoder.EncodedSize(plan.ContentW, plan.ContentH, plan.Format);
        var blocks = ArrayPool<byte>.Shared.Rent(encodedSize);
        try
        {
            var span = blocks.AsSpan(0, encodedSize);
            Bc7Encoder.Encode(content, plan.ContentW, plan.ContentH, srcStride, span, plan.Format, hasAlpha: plan.HasAlpha);
            return CreateBcFromBlocks(span, plan.ContentW, plan.ContentH, plan.Format, options,
                originalWidth, originalHeight, plan.ContentX, plan.ContentY);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(blocks);
        }
    }

    public ITextureRegion LoadBc7(ReadOnlySpan<byte> rgba,
        int width,
        int height,
        int srcStride,
        TextureOptions textureOptions = null,
        Bc7Encoder.Quality quality = Bc7Encoder.Quality.Basic,
        bool hasAlpha = true)
    {
        if (width <= 0 || height <= 0) return null;
        textureOptions ??= TextureOptions.Default;

        if (!deviceContext.HasBcCompression)
        {
            var fallback = CreateInternal(width, height, textureOptions, null);
            fallback.Update(rgba, width, height, 0, 0, srcStride);
            return fallback;
        }

        var encodedSize = Bc7Encoder.EncodedSize(width, height, Bc7Encoder.BcFormat.Bc7);
        var blocks = ArrayPool<byte>.Shared.Rent(encodedSize);
        try
        {
            var span = blocks.AsSpan(0, encodedSize);
            Bc7Encoder.Encode(rgba, width, height, srcStride, span, Bc7Encoder.BcFormat.Bc7, quality, hasAlpha);
            return CreateBcFromBlocks(span, width, height, Bc7Encoder.BcFormat.Bc7, textureOptions, width, height, 0, 0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(blocks);
        }
    }

    internal ITextureRegion CreateBcFromBlocks(scoped ReadOnlySpan<byte> blocks,
        int contentWidth,
        int contentHeight,
        Bc7Encoder.BcFormat format,
        TextureOptions options,
        int originalWidth,
        int originalHeight,
        int contentX,
        int contentY)
    {
        var physWidth = (contentWidth + 3) & ~3;
        var physHeight = (contentHeight + 3) & ~3;

        var texture = CreateBcInternal(physWidth, physHeight, format, options);
        UploadBc(texture, blocks, physWidth, physHeight, format);

        return WrapBc(texture, contentWidth, contentHeight, originalWidth, originalHeight, contentX, contentY);
    }

    internal ITextureRegion EncodeBcOnGpu(ReadOnlySpan<byte> rgba, int originalWidth, int originalHeight, BcPlan plan, TextureOptions options)
    {
        if (!deviceContext.HasBcCompression) return null;

        var physWidth = (plan.ContentW + 3) & ~3;
        var physHeight = (plan.ContentH + 3) & ~3;

        var texture = CreateBcInternal(physWidth, physHeight, plan.Format, options);

        var srcStride = originalWidth * 4;
        var offset = plan.ContentY * srcStride + plan.ContentX * 4;
        ComputeEncoder.EncodeInto(texture, rgba.Slice(offset), plan.ContentW, plan.ContentH, srcStride, plan.Format);

        return WrapBc(texture, plan.ContentW, plan.ContentH, originalWidth, originalHeight, plan.ContentX, plan.ContentY);
    }

    ITextureRegion WrapBc(WebGpuTexture texture, int contentWidth, int contentHeight, int originalWidth, int originalHeight, int contentX, int contentY)
    {
        var trimmed = contentWidth != originalWidth || contentHeight != originalHeight || contentX != 0 || contentY != 0;
        if (trimmed)
            return new WebGpuTrimmedRegion(texture, originalWidth, originalHeight,
                new Rectangle(contentX, contentY, contentWidth, contentHeight));

        return texture.Width == contentWidth && texture.Height == contentHeight
            ? texture
            : new WebGpuTextureRegion(texture, contentWidth, contentHeight);
    }

    WebGpuTexture CreateBcInternal(int physWidth, int physHeight, Bc7Encoder.BcFormat format, TextureOptions options)
    {
        var samplerEntry = samplerCache.Get(options);
        var wgpuFormat = format == Bc7Encoder.BcFormat.Bc1
            ? options.Srgb ? WGPUTextureFormat.BC1RGBAUnormSrgb : WGPUTextureFormat.BC1RGBAUnorm
            : options.Srgb ? WGPUTextureFormat.BC7RGBAUnormSrgb : WGPUTextureFormat.BC7RGBAUnorm;

        var descriptor = new TextureDescriptor
        {
            Size = new()
            {
                width = (uint)physWidth,
                height = (uint)physHeight,
                depthOrArrayLayers = 1
            },
            Format = wgpuFormat,
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
            Dimension = WGPUTextureDimension._2D,
            MipLevelCount = 1,
            SampleCount = 1
        };

        var wgpuTex = deviceContext.Device.CreateTexture(in descriptor);
        var viewDesc = new TextureViewDescriptor
        {
            Format = wgpuFormat,
            Dimension = WGPUTextureViewDimension._2D,
            BaseMipLevel = 0,
            MipLevelCount = 1,
            BaseArrayLayer = 0,
            ArrayLayerCount = 1,
            Aspect = WGPUTextureAspect.All,
            Usage = TextureUsage.TextureBinding
        };

        var view = wgpuTex.CreateView(in viewDesc);

        return new(deviceContext, backend, samplerEntry, wgpuTex, view, physWidth, physHeight, options);
    }

    void UploadBc(WebGpuTexture texture, scoped ReadOnlySpan<byte> tightBlocks, int physWidth, int physHeight, Bc7Encoder.BcFormat format)
    {
        deviceContext.TextureStager.UploadCompressedBlocks(texture, tightBlocks, physWidth, physHeight, Bc7Encoder.BlockBytes(format));
    }

    public IWritableTexture Create(Color color, int width = 1, int height = 1, TextureOptions textureOptions = null)
    {
        textureOptions ??= TextureOptions.Default;
        var texture = CreateInternal(width, height, textureOptions, color);
        texture.Update(color, 0, 0, width, height);
        return texture;
    }

    public IWritableTexture CreateEmpty(int width, int height, TextureOptions textureOptions = null)
    {
        textureOptions ??= TextureOptions.Default;
        return CreateInternal(width, height, textureOptions, null);
    }

    WebGpuTexture CreateInternal(int width, int height, TextureOptions options, Color? clearColor)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        var samplerEntry = samplerCache.Get(options);
        var wantsMips = samplerEntry.Key.HasMips || options.GenerateMipmaps;
        var mipLevels = wantsMips ? ComputeMipLevels(width, height) : 1u;

        var format = options.Srgb ? WGPUTextureFormat.RGBA8UnormSrgb : WGPUTextureFormat.RGBA8Unorm;
        var usage = TextureUsage.TextureBinding | TextureUsage.CopyDst | TextureUsage.CopySrc;
        if (mipLevels > 1)
        {
            usage |= TextureUsage.RenderAttachment;
        }

        var descriptor = new TextureDescriptor
        {
            Size = new()
            {
                width = (uint)width,
                height = (uint)height,
                depthOrArrayLayers = 1
            },
            Format = format,
            Usage = usage,
            Dimension = WGPUTextureDimension._2D,
            MipLevelCount = mipLevels,
            SampleCount = 1
        };

        var wgpuTex = deviceContext.Device.CreateTexture(in descriptor);
        var viewDesc = new TextureViewDescriptor
        {
            Format = format,
            Dimension = WGPUTextureViewDimension._2D,
            BaseMipLevel = 0,
            MipLevelCount = mipLevels,
            BaseArrayLayer = 0,
            ArrayLayerCount = 1,
            Aspect = WGPUTextureAspect.All,
            Usage = TextureUsage.TextureBinding
        };

        var view = wgpuTex.CreateView(in viewDesc);

        return new(deviceContext, backend, samplerEntry, wgpuTex, view, width, height, options);
    }


    static uint ComputeMipLevels(int width, int height)
    {
        var maxDim = Math.Max(width, height);
        uint levels = 1;
        while (maxDim > 1)
        {
            maxDim >>= 1;
            ++levels;
        }

        return levels;
    }
}