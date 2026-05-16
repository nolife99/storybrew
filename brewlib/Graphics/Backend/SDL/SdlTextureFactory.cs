namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Textures;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public sealed class SdlTextureFactory(SdlGraphicsBackend backend) : ITextureFactory
{
    public ITextureRegion Load(Image<Rgba32> bitmap, TextureOptions textureOptions = null)
        => SdlTexture.Load(backend, bitmap, textureOptions);

    public IWritableTexture Create(Color color,
        int width = 1,
        int height = 1,
        TextureOptions textureOptions = null)
        => SdlTexture.Create(backend, color, width, height, textureOptions);
}

public sealed class SdlTexture : Texture2dRegion, IWritableTexture
{
    readonly SdlGraphicsBackend backend;
    readonly TextureOptions textureOptions;

    nint textureHandle, samplerHandle;
    bool disposedTexture;

    SdlTexture(SdlGraphicsBackend backend,
        nint textureHandle,
        nint samplerHandle,
        int width,
        int height,
        TextureOptions textureOptions)
        : base(null, new(0, 0, width, height))
    {
        this.backend = backend;
        this.textureHandle = textureHandle;
        this.samplerHandle = samplerHandle;
        this.textureOptions = textureOptions;
    }

    public IGraphicsBackend Backend => backend;
    public GraphicsResourceHandle NativeHandle => new(backend.Name, textureHandle);
    public nint TextureHandle => textureHandle;
    public nint SamplerHandle => samplerHandle;

    public static SdlTexture Create(SdlGraphicsBackend backend,
        Color color,
        int width,
        int height,
        TextureOptions textureOptions = null)
    {
        if (width < 1 || height < 1) throw new InvalidOperationException($"Invalid texture size: {width}x{height}");

        textureOptions ??= TextureOptions.Default;
        if (textureOptions.PreMultiply)
        {
            var vec = color.ToScaledVector4();
            color = Color.FromScaledVector(new(vec.AsVector3() * vec.W, vec.W));
        }

        var texture = createEmptyTexture(backend, width, height, textureOptions);
        var pixels = new Rgba32[width * height];
        pixels.AsSpan().Fill(color.ToPixel<Rgba32>());
        texture.upload(pixels, width, height, 0, 0, textureOptions.GenerateMipmaps);
        return texture;
    }

    public static SdlTexture Load(SdlGraphicsBackend backend,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions = null)
    {
        var width = int.Min(backend.Capabilities.MaxTextureSize, bitmap.Width);
        var height = int.Min(backend.Capabilities.MaxTextureSize, bitmap.Height);

        textureOptions ??= TextureOptions.Default;
        var texture = createEmptyTexture(backend, width, height, textureOptions);

        var pixels = new Rgba32[width * height];
        var buffer = bitmap.Frames.RootFrame.PixelBuffer;
        for (var y = 0; y < height; ++y)
            buffer.DangerousGetRowSpan(y)[..width].CopyTo(pixels.AsSpan(y * width, width));

        texture.upload(pixels, width, height, 0, 0, textureOptions.GenerateMipmaps);
        return texture;
    }

    public void Update(Color color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

        var pixel = color.ToPixel<Rgba32>();
        var pixels = new Rgba32[width * height];
        pixels.AsSpan().Fill(pixel);
        upload(pixels, width, height, x, y, textureOptions.GenerateMipmaps);
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

        var pixels = new Rgba32[bitmap.Width * bitmap.Height];
        var buffer = bitmap.Frames.RootFrame.PixelBuffer;
        for (var row = 0; row < bitmap.Height; ++row)
            buffer.DangerousGetRowSpan(row).CopyTo(pixels.AsSpan(row * bitmap.Width, bitmap.Width));

        upload(pixels, bitmap.Width, bitmap.Height, x, y, textureOptions.GenerateMipmaps);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposedTexture)
        {
            if (textureHandle != nint.Zero) SDL.ReleaseGPUTexture(backend.DeviceHandle, textureHandle);
            if (samplerHandle != nint.Zero) SDL.ReleaseGPUSampler(backend.DeviceHandle, samplerHandle);

            textureHandle = nint.Zero;
            samplerHandle = nint.Zero;
            disposedTexture = true;
        }

        base.Dispose(disposing);
    }

    static SdlTexture createEmptyTexture(SdlGraphicsBackend backend,
        int width,
        int height,
        TextureOptions textureOptions)
    {
        var createInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.Texturetype2D,
            Format = textureOptions.Srgb ? SDL.GPUTextureFormat.R8G8B8A8UnormSRGB : SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Usage = SDL.GPUTextureUsageFlags.Sampler,
            Width = (uint)width,
            Height = (uint)height,
            LayerCountOrDepth = 1,
            NumLevels = textureOptions.GenerateMipmaps ? getMipLevelCount(width, height) : 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1
        };

        var textureHandle = SDL.CreateGPUTexture(backend.DeviceHandle, in createInfo);
        if (textureHandle == nint.Zero)
            throw new InvalidOperationException($"Unable to create SDL GPU texture: {SDL.GetError()}");

        var samplerInfo = createSamplerInfo(textureOptions);
        var samplerHandle = SDL.CreateGPUSampler(backend.DeviceHandle, in samplerInfo);
        if (samplerHandle == nint.Zero)
        {
            SDL.ReleaseGPUTexture(backend.DeviceHandle, textureHandle);
            throw new InvalidOperationException($"Unable to create SDL GPU sampler: {SDL.GetError()}");
        }

        return new(backend, textureHandle, samplerHandle, width, height, textureOptions);
    }

    void upload(ReadOnlySpan<Rgba32> pixels,
        int width,
        int height,
        int x,
        int y,
        bool generateMipmaps)
    {
        var bytes = MemoryMarshal.AsBytes(pixels).ToArray();
        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)bytes.Length
        };

        var transferBuffer = SDL.CreateGPUTransferBuffer(backend.DeviceHandle, in transferCreateInfo);

        if (transferBuffer == nint.Zero)
            throw new InvalidOperationException($"Unable to create SDL GPU texture transfer buffer: {SDL.GetError()}");

        try
        {
            var mapped = SDL.MapGPUTransferBuffer(backend.DeviceHandle, transferBuffer, false);
            if (mapped == nint.Zero)
                throw new InvalidOperationException($"Unable to map SDL GPU texture transfer buffer: {SDL.GetError()}");

            Marshal.Copy(bytes, 0, mapped, bytes.Length);
            SDL.UnmapGPUTransferBuffer(backend.DeviceHandle, transferBuffer);

            var commandBuffer = SDL.AcquireGPUCommandBuffer(backend.DeviceHandle);
            var copyPass = SDL.BeginGPUCopyPass(commandBuffer);

            var source = new SDL.GPUTextureTransferInfo
            {
                TransferBuffer = transferBuffer,
                PixelsPerRow = (uint)width,
                RowsPerLayer = (uint)height
            };

            var destination = new SDL.GPUTextureRegion
            {
                Texture = textureHandle,
                W = (uint)width,
                H = (uint)height,
                D = 1,
                X = (uint)x,
                Y = (uint)y
            };

            SDL.UploadToGPUTexture(copyPass, in source, in destination, true);
            SDL.EndGPUCopyPass(copyPass);
            if (generateMipmaps) SDL.GenerateMipmapsForGPUTexture(commandBuffer, textureHandle);

            if (!SDL.SubmitGPUCommandBuffer(commandBuffer))
                throw new InvalidOperationException($"Unable to submit SDL GPU texture upload: {SDL.GetError()}");

            // CRITICAL: Wait for upload to complete before the texture is used in rendering
            // Without this, there's a race condition where rendering may sample from incomplete textures
            SDL.WaitForGPUIdle(backend.DeviceHandle);
        }
        finally
        {
            SDL.ReleaseGPUTransferBuffer(backend.DeviceHandle, transferBuffer);
        }
    }

    static SDL.GPUSamplerCreateInfo createSamplerInfo(TextureOptions options)
        => new()
        {
            MinFilter = toFilter(options.TextureMinFilter),
            MagFilter = toFilter(options.TextureMagFilter),
            MipmapMode = toMipmapMode(options.TextureMinFilter),
            AddressModeU = toAddressMode(options.TextureWrapS),
            AddressModeV = toAddressMode(options.TextureWrapT),
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
            MaxLod = float.MaxValue
        };

    static SDL.GPUFilter toFilter(TextureFilter filter)
        => filter is TextureFilter.Nearest or TextureFilter.NearestMipmapNearest or TextureFilter.NearestMipmapLinear ?
            SDL.GPUFilter.Nearest :
            SDL.GPUFilter.Linear;

    static SDL.GPUSamplerMipmapMode toMipmapMode(TextureFilter filter)
        => filter is TextureFilter.NearestMipmapNearest or TextureFilter.LinearMipmapNearest ?
            SDL.GPUSamplerMipmapMode.Nearest :
            SDL.GPUSamplerMipmapMode.Linear;

    static SDL.GPUSamplerAddressMode toAddressMode(TextureWrap wrap)
        => wrap switch
        {
            TextureWrap.Repeat => SDL.GPUSamplerAddressMode.Repeat,
            TextureWrap.MirroredRepeat => SDL.GPUSamplerAddressMode.MirroredRepeat,
            _ => SDL.GPUSamplerAddressMode.ClampToEdge
        };

    static uint getMipLevelCount(int width, int height)
        => (uint)(int.Log2(int.Max(width, height)) + 1);
}
