namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using BasisBlockEncoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Textures;

sealed class WebGpuTextureFactory : ITextureFactory
{
    readonly IGraphicsBackend backend;
    readonly WebGpuDeviceContext deviceContext;
    readonly WebGpuSamplerCache samplerCache;

    public WebGpuTextureFactory(WebGpuDeviceContext deviceContext,
        WebGpuSamplerCache samplerCache,
        IGraphicsBackend backend)
    {
        this.deviceContext = deviceContext;
        this.samplerCache = samplerCache;
        this.backend = backend;
    }

    public ITextureRegion Load(Image<Rgba32> bitmap, TextureOptions textureOptions = null)
    {
        if (bitmap is null) return null;

        textureOptions ??= TextureOptions.Default;

        if (TryPlanBc(bitmap, textureOptions, out var plan))
        {
            var region = LoadBcStreamed(bitmap, plan, textureOptions);
            if (region is not null) return region;
        }

        var texture = CreateInternal(bitmap.Width, bitmap.Height, textureOptions);

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

    public static int Bc7MinArea { get; set; } = 4 * 4;
    public static bool TrimTransparent { get; set; } = true;

    internal bool TryPlanBc(Image<Rgba32> bitmap, TextureOptions options, out BcPlan plan)
        => BcImageEncoder.TryPlan(bitmap,
               deviceContext.HasBcCompression ? TextureCompressionFormats.Bc : TextureCompressionFormats.None,
               new(Bc7MinArea, TrimTransparent), options, out plan);

    internal ITextureRegion LoadBcStreamed(Image<Rgba32> bitmap, BcPlan plan, TextureOptions options)
    {
        var physWidth = (plan.ContentW + 3) & ~3;
        var physHeight = (plan.ContentH + 3) & ~3;

        var texture = CreateBcInternal(physWidth, physHeight, plan.Format, options);
        deviceContext.TextureStager.UploadCompressedStreaming(texture, physWidth, physHeight,
            BlockEncoder.BlockBytes(plan.Format),
            (blockRowStart, bandRows, dst) => BcImageEncoder.EncodeBand(bitmap, in plan, blockRowStart, bandRows, dst));

        return WrapBc(texture, plan.ContentW, plan.ContentH, bitmap.Width, bitmap.Height, plan.ContentX, plan.ContentY);
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

    WebGpuTexture CreateBcInternal(int physWidth, int physHeight, BcFormat format, TextureOptions options)
    {
        var samplerEntry = samplerCache.Get(options);
        var wgpuFormat = format == BcFormat.Bc1
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

    public IWritableTexture Create(Color color, int width = 1, int height = 1, TextureOptions textureOptions = null)
    {
        textureOptions ??= TextureOptions.Default;
        var texture = CreateInternal(width, height, textureOptions);
        texture.Update(color, 0, 0, width, height);
        return texture;
    }

    public IWritableTexture CreateEmpty(int width, int height, TextureOptions textureOptions = null)
    {
        textureOptions ??= TextureOptions.Default;
        return CreateInternal(width, height, textureOptions);
    }

    WebGpuTexture CreateInternal(int width, int height, TextureOptions options)
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