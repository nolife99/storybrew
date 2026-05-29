namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
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