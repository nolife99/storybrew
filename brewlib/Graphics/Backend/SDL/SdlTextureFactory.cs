namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Textures;
using Util;

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

public sealed class SdlAsyncTextureUploader(SdlGraphicsBackend backend)
    : AsyncTextureUploaderBase(() => backend.Capabilities.MaxTextureSize)
{
    protected override ValueTask<PreparedTextureUpload> PrepareAsync(string source,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions,
        CancellationToken cancellationToken)
    {
        var description = CreateDescription(source, bitmap, textureOptions);
        SdlPreparedTextureUpload upload = null;

        try
        {
            upload = SdlPreparedTextureUpload.Create(description);
            CopyBitmapRows(bitmap, upload);
            cancellationToken.ThrowIfCancellationRequested();
            return new(upload);
        }
        catch
        {
            upload?.Dispose();
            throw;
        }
    }

    public override ITextureRegion Upload(PreparedTextureUpload upload)
        => upload is SdlPreparedTextureUpload sdlUpload ?
            SdlTexture.Load(backend, sdlUpload) :
            throw new InvalidOperationException("SDL async texture uploader received an upload prepared by another backend");
}

sealed class SdlPreparedTextureUpload : PreparedTextureUpload
{
    bool disposed;
    IMemoryOwner<Rgba32> pixels;

    SdlPreparedTextureUpload(TextureUploadDescription description,
        IMemoryOwner<Rgba32> pixels)
        : base(description)
        => this.pixels = pixels;

    internal override Span<byte> WritableBytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return MemoryMarshal.AsBytes(WritablePixels);
        }
    }

    internal ReadOnlySpan<Rgba32> ReadablePixels => WritablePixels;

    Span<Rgba32> WritablePixels
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return pixels.Memory.Span[..checked(Width * Height)];
        }
    }

    internal static SdlPreparedTextureUpload Create(TextureUploadDescription description)
        => new(description, MemoryAllocator.Default.Allocate<Rgba32>(checked(description.Width * description.Height)));

    public override void Dispose()
    {
        if (disposed) return;

        disposed = true;

        pixels.Dispose();
        pixels = null;
    }
}

public sealed class SdlTexture : Texture2dRegion, IWritableTexture
{
    readonly SdlGraphicsBackend backend;
    readonly TextureOptions textureOptions;
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
        TextureHandle = textureHandle;
        SamplerHandle = samplerHandle;
        this.textureOptions = textureOptions;
    }

    public nint TextureHandle { get; private set; }

    public nint SamplerHandle { get; private set; }

    public IGraphicsBackend Backend => backend;
    public GraphicsResourceHandle NativeHandle => new(backend.Name, TextureHandle);

    public void Update(Color color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

        var pixel = color.ToPixel<Rgba32>();
        uploadColor(pixel, width, height, x, y, textureOptions.GenerateMipmaps, true);
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

        uploadBitmap(bitmap, bitmap.Width, bitmap.Height, x, y, textureOptions.GenerateMipmaps, true);
    }

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
        texture.uploadColor(color.ToPixel<Rgba32>(),
            width,
            height,
            0,
            0,
            textureOptions.GenerateMipmaps,
            false);

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

        texture.uploadBitmap(bitmap, width, height, 0, 0, textureOptions.GenerateMipmaps, false);
        return texture;
    }

    internal static SdlTexture Load(SdlGraphicsBackend backend,
        SdlPreparedTextureUpload upload)
    {
        if (upload.Width < 1 || upload.Height < 1)
            throw new InvalidOperationException($"Invalid texture size: {upload.Width}x{upload.Height}");

        if (upload.Format != TextureUploadFormat.Rgba8)
            throw new NotSupportedException($"Unsupported SDL texture upload format: {upload.Format}");

        var textureOptions = upload.Options ?? TextureOptions.Default;
        var texture = createEmptyTexture(backend, upload.Width, upload.Height, textureOptions);

        try
        {
            texture.uploadPrepared(upload, textureOptions.GenerateMipmaps, false);
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposedTexture)
        {
            backend.ReleaseTexture(TextureHandle);
            backend.ReleaseSampler(SamplerHandle);

            TextureHandle = nint.Zero;
            SamplerHandle = nint.Zero;
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
            backend.ReleaseTexture(textureHandle);
            throw new InvalidOperationException($"Unable to create SDL GPU sampler: {SDL.GetError()}");
        }

        return new(backend, textureHandle, samplerHandle, width, height, textureOptions);
    }

    void uploadColor(Rgba32 pixel,
        int width,
        int height,
        int x,
        int y,
        bool generateMipmaps,
        bool flushRenderer)
    {
        var transferBuffer = createTransferBuffer(width, height, out var bytesPerRow);

        try
        {
            var mapped = SDL.MapGPUTransferBuffer(backend.DeviceHandle, transferBuffer, false);
            if (mapped == nint.Zero)
                throw new InvalidOperationException($"Unable to map SDL GPU texture transfer buffer: {SDL.GetError()}");

            var pixelsPerRow = bytesPerRow / Unsafe.SizeOf<Rgba32>();
            var pixels = mapped.AsSpan<Rgba32>(checked(pixelsPerRow * height));
            for (var row = 0; row < height; ++row)
                pixels.Slice(row * pixelsPerRow, width).Fill(pixel);

            SDL.UnmapGPUTransferBuffer(backend.DeviceHandle, transferBuffer);

            uploadMappedTransferBuffer(transferBuffer, width, height, x, y, generateMipmaps, flushRenderer);
        }
        finally
        {
            backend.ReleaseTransferBuffer(transferBuffer);
        }
    }

    void uploadBitmap(Image<Rgba32> bitmap,
        int width,
        int height,
        int x,
        int y,
        bool generateMipmaps,
        bool flushRenderer)
    {
        var transferBuffer = createTransferBuffer(width, height, out var bytesPerRow);

        try
        {
            var mapped = SDL.MapGPUTransferBuffer(backend.DeviceHandle, transferBuffer, false);
            if (mapped == nint.Zero)
                throw new InvalidOperationException($"Unable to map SDL GPU texture transfer buffer: {SDL.GetError()}");

            var target = mapped.AsSpan<byte>(checked(bytesPerRow * height));
            var source = bitmap.Frames.RootFrame.PixelBuffer;
            var sourceRowBytes = checked(width * Unsafe.SizeOf<Rgba32>());
            for (var row = 0; row < height; ++row)
            {
                var sourceRow = MemoryMarshal.AsBytes(source.DangerousGetRowSpan(row)[..width]);
                sourceRow.CopyTo(target.Slice(row * bytesPerRow, sourceRowBytes));
            }

            SDL.UnmapGPUTransferBuffer(backend.DeviceHandle, transferBuffer);

            uploadMappedTransferBuffer(transferBuffer, width, height, x, y, generateMipmaps, flushRenderer);
        }
        finally
        {
            backend.ReleaseTransferBuffer(transferBuffer);
        }
    }

    void uploadPrepared(SdlPreparedTextureUpload upload,
        bool generateMipmaps,
        bool flushRenderer)
    {
        var transferBuffer = createTransferBuffer(upload.Width, upload.Height, out var bytesPerRow);

        try
        {
            var mapped = SDL.MapGPUTransferBuffer(backend.DeviceHandle, transferBuffer, false);
            if (mapped == nint.Zero)
                throw new InvalidOperationException($"Unable to map SDL GPU texture transfer buffer: {SDL.GetError()}");

            var target = mapped.AsSpan<byte>(checked(bytesPerRow * upload.Height));
            var source = upload.ReadablePixels;
            var sourceRowBytes = checked(upload.Width * Unsafe.SizeOf<Rgba32>());
            for (var row = 0; row < upload.Height; ++row)
            {
                var sourceRow = MemoryMarshal.AsBytes(source.Slice(row * upload.Width, upload.Width));
                sourceRow.CopyTo(target.Slice(row * bytesPerRow, sourceRowBytes));
            }

            SDL.UnmapGPUTransferBuffer(backend.DeviceHandle, transferBuffer);

            uploadMappedTransferBuffer(transferBuffer,
                upload.Width,
                upload.Height,
                0,
                0,
                generateMipmaps,
                flushRenderer);
        }
        finally
        {
            backend.ReleaseTransferBuffer(transferBuffer);
        }
    }

    nint createTransferBuffer(int width, int height, out int bytesPerRow)
    {
        bytesPerRow = getUploadBytesPerRow(width);
        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)checked(bytesPerRow * height)
        };

        var transferBuffer = SDL.CreateGPUTransferBuffer(backend.DeviceHandle, in transferCreateInfo);
        if (transferBuffer == nint.Zero)
            throw new InvalidOperationException($"Unable to create SDL GPU texture transfer buffer: {SDL.GetError()}");

        return transferBuffer;
    }

    void uploadMappedTransferBuffer(nint transferBuffer,
        int width,
        int height,
        int x,
        int y,
        bool generateMipmaps,
        bool flushRenderer)
    {
        if (flushRenderer && backend.HasActiveFrame)
            backend.PrepareCopyFromDraw();

        backend.UploadTexture(transferBuffer,
            TextureHandle,
            width,
            height,
            0,
            0,
            x,
            y,
            generateMipmaps,
            nameof(SdlTexture));
    }

    int getUploadBytesPerRow(int width)
        => checked(width * Unsafe.SizeOf<Rgba32>());

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