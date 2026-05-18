namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
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

public sealed class SdlAsyncTextureUploader(SdlGraphicsBackend backend)
    : AsyncTextureUploaderBase(() => backend.Capabilities.MaxTextureSize)
{
    protected override async ValueTask<PreparedTextureUpload> PrepareAsync(string source,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions,
        CancellationToken cancellationToken)
    {
        var description = CreateDescription(source, bitmap, textureOptions);
        var state = new CreateUploadState(backend, description);
        SdlPreparedTextureUpload upload = null;

        try
        {
            await RunOnMainThread(static s => ((CreateUploadState)s).Run(), state, cancellationToken)
                .ConfigureAwait(false);

            upload = state.Upload;
            CopyBitmapRows(bitmap, upload);
            return upload;
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

    sealed class CreateUploadState(SdlGraphicsBackend backend, TextureUploadDescription description)
    {
        public SdlPreparedTextureUpload Upload { get; private set; }

        public void Run() => Upload = SdlPreparedTextureUpload.Create(backend, description);
    }
}

internal sealed class SdlPreparedTextureUpload : PreparedTextureUpload
{
    readonly SdlGraphicsBackend backend;
    nint transferBuffer, mapped;
    bool disposed;

    SdlPreparedTextureUpload(SdlGraphicsBackend backend,
        TextureUploadDescription description,
        nint transferBuffer,
        nint mapped)
        : base(description)
    {
        this.backend = backend;
        this.transferBuffer = transferBuffer;
        this.mapped = mapped;
    }

    internal override Span<byte> WritableBytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (mapped == nint.Zero) throw new InvalidOperationException("SDL texture upload is not mapped");

            return mapped.AsSpan<byte>(ByteLength);
        }
    }

    internal static SdlPreparedTextureUpload Create(SdlGraphicsBackend backend,
        TextureUploadDescription description)
    {
        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)description.ByteLength
        };

        var transferBuffer = SDL.CreateGPUTransferBuffer(backend.DeviceHandle, in transferCreateInfo);
        if (transferBuffer == nint.Zero)
            throw new InvalidOperationException($"Unable to create SDL GPU texture transfer buffer: {SDL.GetError()}");

        var mapped = SDL.MapGPUTransferBuffer(backend.DeviceHandle, transferBuffer, false);
        if (mapped != nint.Zero) return new(backend, description, transferBuffer, mapped);

        backend.ReleaseTransferBuffer(transferBuffer);
        throw new InvalidOperationException($"Unable to map SDL GPU texture transfer buffer: {SDL.GetError()}");
    }

    internal nint UnmapAndDetach()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (mapped != nint.Zero)
        {
            SDL.UnmapGPUTransferBuffer(backend.DeviceHandle, transferBuffer);
            mapped = nint.Zero;
        }

        var detached = transferBuffer;
        transferBuffer = nint.Zero;
        return detached;
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;

        if (SDL.IsMainThread())
            releaseResources();
        else if (Native.MainThreadScheduler is not null)
            _ = Native.MainThreadScheduler(static s => ((SdlPreparedTextureUpload)s).releaseResources(), this)
                .AsTask();
        else
            releaseResources();
    }

    void releaseResources()
    {
        if (mapped != nint.Zero && transferBuffer != nint.Zero)
        {
            SDL.UnmapGPUTransferBuffer(backend.DeviceHandle, transferBuffer);
            mapped = nint.Zero;
        }

        if (transferBuffer == nint.Zero) return;

        backend.ReleaseTransferBuffer(transferBuffer);
        transferBuffer = nint.Zero;
    }
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
        texture.uploadColor(color.ToPixel<Rgba32>(),
            width,
            height,
            0,
            0,
            textureOptions.GenerateMipmaps,
            flushRenderer: false);
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

        texture.uploadBitmap(bitmap, width, height, 0, 0, textureOptions.GenerateMipmaps, flushRenderer: false);
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
        var transferBuffer = nint.Zero;

        try
        {
            transferBuffer = upload.UnmapAndDetach();
            texture.uploadMappedTransferBuffer(transferBuffer,
                upload.Width,
                upload.Height,
                0,
                0,
                textureOptions.GenerateMipmaps,
                flushRenderer: false);
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
        finally
        {
            if (transferBuffer != nint.Zero)
                backend.ReleaseTransferBuffer(transferBuffer);
        }
    }

    public void Update(Color color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

        var pixel = color.ToPixel<Rgba32>();
        uploadColor(pixel, width, height, x, y, textureOptions.GenerateMipmaps, flushRenderer: true);
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

        uploadBitmap(bitmap, bitmap.Width, bitmap.Height, x, y, textureOptions.GenerateMipmaps, flushRenderer: true);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposedTexture)
        {
            backend.ReleaseTexture(textureHandle);
            backend.ReleaseSampler(samplerHandle);

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
        var transferBuffer = createTransferBuffer(width, height);

        try
        {
            var mapped = SDL.MapGPUTransferBuffer(backend.DeviceHandle, transferBuffer, false);
            if (mapped == nint.Zero)
                throw new InvalidOperationException($"Unable to map SDL GPU texture transfer buffer: {SDL.GetError()}");

            mapped.AsSpan<Rgba32>(checked(width * height)).Fill(pixel);
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
        var transferBuffer = createTransferBuffer(width, height);

        try
        {
            var mapped = SDL.MapGPUTransferBuffer(backend.DeviceHandle, transferBuffer, false);
            if (mapped == nint.Zero)
                throw new InvalidOperationException($"Unable to map SDL GPU texture transfer buffer: {SDL.GetError()}");

            var pixels = mapped.AsSpan<Rgba32>(checked(width * height));
            var source = bitmap.Frames.RootFrame.PixelBuffer;
            for (var row = 0; row < height; ++row)
                source.DangerousGetRowSpan(row)[..width].CopyTo(pixels.Slice(row * width, width));

            SDL.UnmapGPUTransferBuffer(backend.DeviceHandle, transferBuffer);

            uploadMappedTransferBuffer(transferBuffer, width, height, x, y, generateMipmaps, flushRenderer);
        }
        finally
        {
            backend.ReleaseTransferBuffer(transferBuffer);
        }
    }

    nint createTransferBuffer(int width, int height)
    {
        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)checked(width * height * Unsafe.SizeOf<Rgba32>())
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
            textureHandle,
            width,
            height,
            x,
            y,
            generateMipmaps,
            nameof(SdlTexture));
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
