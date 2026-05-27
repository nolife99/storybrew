namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Textures;
using Color = SixLabors.ImageSharp.Color;

public sealed class WebGpuTextureFactory(WebGpuGraphicsBackend backend) : ITextureFactory
{
    public ITextureRegion Load(Image<Rgba32> bitmap, TextureOptions textureOptions = null)
        => WebGpuTexture.Load(backend, bitmap, textureOptions);

    public IWritableTexture Create(Color color,
        int width = 1,
        int height = 1,
        TextureOptions textureOptions = null)
        => WebGpuTexture.Create(backend, color, width, height, textureOptions);
}

public sealed class WebGpuAsyncTextureUploader(WebGpuGraphicsBackend backend)
    : AsyncTextureUploaderBase(() => backend.Capabilities.MaxTextureSize)
{
    protected override ValueTask<PreparedTextureUpload> PrepareAsync(string source,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions,
        CancellationToken cancellationToken)
    {
        var description = CreateDescription(source, bitmap, textureOptions);
        cancellationToken.ThrowIfCancellationRequested();
        return new(WebGpuPreparedTextureUpload.Create(description, bitmap));
    }

    public override ITextureRegion Upload(PreparedTextureUpload upload)
    {
        if (upload is not WebGpuPreparedTextureUpload webGpuUpload)
            throw new InvalidOperationException("WebGPU async texture uploader received an upload prepared by another backend");

        try
        {
            return WebGpuTexture.Load(backend, webGpuUpload);
        }
        catch
        {
            webGpuUpload.Dispose();
            throw;
        }
    }
}

sealed class WebGpuPreparedTextureUpload : PreparedTextureUpload, IPreparedTextureUploadOwnsBitmap
{
    Image<Rgba32> bitmap;
    bool disposed;

    WebGpuPreparedTextureUpload(TextureUploadDescription description, Image<Rgba32> bitmap)
        : base(description)
        => this.bitmap = bitmap;

    internal Image<Rgba32> Bitmap
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return bitmap;
        }
    }

    internal static WebGpuPreparedTextureUpload Create(TextureUploadDescription description, Image<Rgba32> bitmap)
        => new(description, bitmap);

    public override void Dispose()
    {
        if (disposed) return;

        disposed = true;
        bitmap?.Dispose();
        bitmap = null;
    }
}

public sealed class WebGpuTexture : Texture2dRegion, IWritableTexture, ITextureSamplerIdentity
{
    readonly WebGpuGraphicsBackend backend;
    readonly nint samplerIdentity;
    readonly TextureOptions textureOptions;
    bool disposedTexture;

    WebGpuTexture(WebGpuGraphicsBackend backend,
        Texture texture,
        TextureView textureView,
        Sampler sampler,
        WGPUTextureFormat textureFormat,
        int width,
        int height,
        TextureOptions textureOptions)
        : base(null, new(0, 0, width, height))
    {
        this.backend = backend;
        TextureHandle = texture;
        TextureViewHandle = textureView;
        SamplerHandle = sampler;
        TextureFormat = textureFormat;
        this.textureOptions = textureOptions;
        samplerIdentity = getSamplerIdentity(textureOptions);
    }

    public Texture TextureHandle { get; private set; }

    public WGPUTextureFormat TextureFormat { get; private set; }

    public TextureView TextureViewHandle { get; private set; }

    public Sampler SamplerHandle { get; private set; }

    public GraphicsResourceHandle SamplerIdentity => new(backend.Name, samplerIdentity);
    public IGraphicsBackend Backend => backend;
    public GraphicsResourceHandle NativeHandle => new(backend.Name, TextureHandle.NativeHandle());

    public void Update(Color color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);
        backend.ThrowIfDeviceLost();

        uploadColor(getUploadPixel(color, textureOptions), width, height, x, y);
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);
        backend.ThrowIfDeviceLost();

        var width = int.Min(bitmap.Width, Width - x);
        var height = int.Min(bitmap.Height, Height - y);
        uploadBitmapRows(bitmap, width, height, x, y);
    }

    public static WebGpuTexture Create(WebGpuGraphicsBackend backend,
        Color color,
        int width = 1,
        int height = 1,
        TextureOptions textureOptions = null)
    {
        if (width < 1 || height < 1) throw new InvalidOperationException($"Invalid texture size: {width}x{height}");

        backend.ThrowIfDeviceLost();

        textureOptions ??= TextureOptions.Default;
        var texture = createEmptyTexture(backend, width, height, textureOptions);

        try
        {
            texture.uploadColor(getUploadPixel(color, textureOptions), width, height, 0, 0);
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    public static WebGpuTexture Load(WebGpuGraphicsBackend backend,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions = null)
    {
        backend.ThrowIfDeviceLost();

        var width = int.Min(backend.Capabilities.MaxTextureSize, bitmap.Width);
        var height = int.Min(backend.Capabilities.MaxTextureSize, bitmap.Height);

        textureOptions ??= TextureOptions.Default;
        var texture = createEmptyTexture(backend, width, height, textureOptions);

        try
        {
            texture.uploadBitmapRows(bitmap, width, height, 0, 0);
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
    }

    internal static WebGpuTexture Load(WebGpuGraphicsBackend backend, WebGpuPreparedTextureUpload upload)
    {
        if (upload.Width < 1 || upload.Height < 1)
            throw new InvalidOperationException($"Invalid texture size: {upload.Width}x{upload.Height}");

        if (upload.Format != TextureUploadFormat.Rgba8)
            throw new NotSupportedException($"Unsupported WebGPU texture upload format: {upload.Format}");

        backend.ThrowIfDeviceLost();

        var textureOptions = upload.Options ?? TextureOptions.Default;
        var texture = createEmptyTexture(backend, upload.Width, upload.Height, textureOptions);

        try
        {
            texture.uploadBitmapRows(upload.Bitmap, upload.Width, upload.Height, 0, 0);
            upload.Dispose();
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
            backend.PurgeCachedBindGroupsReferencing(WebGpuResourceReference.Of(TextureViewHandle));
            backend.PurgeCachedBindGroupsReferencing(WebGpuResourceReference.Of(SamplerHandle));

            if (!TextureViewHandle.IsNull) backend.DeferredReleases.Retire(TextureViewHandle);
            if (!SamplerHandle.IsNull) backend.DeferredReleases.Retire(SamplerHandle);
            if (!TextureHandle.IsNull) backend.DeferredReleases.Retire(TextureHandle);

            TextureHandle = default;
            TextureViewHandle = default;
            SamplerHandle = default;
            TextureFormat = default;
            disposedTexture = true;
        }

        base.Dispose(disposing);
    }

    static WebGpuTexture createEmptyTexture(WebGpuGraphicsBackend backend,
        int width,
        int height,
        TextureOptions textureOptions)
    {
        TextureDescriptor textureDescriptor = new()
        {
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
            Dimension = WGPUTextureDimension._2D,
            Size = new()
            {
                width = (uint)width,
                height = (uint)height,
                depthOrArrayLayers = 1
            },
            Format = textureOptions.Srgb && DrawState.ColorCorrected
                ? WGPUTextureFormat.RGBA8UnormSrgb
                : WGPUTextureFormat.RGBA8Unorm,
            MipLevelCount = 1,
            SampleCount = 1
        };

        var texture = backend.DeviceHandle.CreateTexture(in textureDescriptor);

        TextureViewDescriptor viewDescriptor = new()
        {
            Format = textureDescriptor.Format,
            Dimension = WGPUTextureViewDimension._2D,
            MipLevelCount = 1,
            ArrayLayerCount = 1,
            Aspect = WGPUTextureAspect.All,
            Usage = TextureUsage.TextureBinding
        };

        TextureView view = default;
        Sampler sampler = default;
        try
        {
            view = texture.CreateView(in viewDescriptor);
            var samplerDescriptor = createSamplerDescriptor(textureOptions);
            sampler = backend.DeviceHandle.CreateSampler(in samplerDescriptor);
            return new(backend, texture, view, sampler, textureDescriptor.Format, width, height, textureOptions);
        }
        catch
        {
            if (!backend.IsDeviceLost)
            {
                if (!sampler.IsNull) sampler.Dispose();
                if (!view.IsNull) view.Dispose();
                if (!texture.IsNull) texture.Dispose();
            }

            throw;
        }
    }

    void uploadColor(Rgba32 pixel, int width, int height, int x, int y)
    {
        if (width <= 0 || height <= 0) return;

        var rowBytes = checked(width * 4);
        var byteLength = checked(rowBytes * height);
        byte[] rented = null;
        var bytes = byteLength <= 4096
            ? stackalloc byte[byteLength]
            : (rented = ArrayPool<byte>.Shared.Rent(byteLength)).AsSpan(0, byteLength);

        try
        {
            var pixels = MemoryMarshal.Cast<byte, Rgba32>(bytes[..byteLength]);
            pixels.Fill(pixel);
            uploadBytes(bytes[..byteLength], width, height, x, y, rowBytes);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    void uploadBitmapRows(Image<Rgba32> bitmap, int width, int height, int x, int y)
    {
        if (width <= 0 || height <= 0) return;

        ObjectDisposedException.ThrowIf(disposedTexture, this);
        backend.ThrowIfDeviceLost();
        backend.UploadTextureRowsWithStagingBelt(TextureHandle, TextureFormat, bitmap, width, height, x, y);
    }


    void uploadBytes(ReadOnlySpan<byte> data, int width, int height, int x, int y, int bytesPerRow)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);
        backend.ThrowIfDeviceLost();
        if (width <= 0 || height <= 0) return;

        bytesPerRow = bytesPerRow <= 0 ? checked(width * 4) : bytesPerRow;
        var rowBytes = checked(width * 4);
        var requiredBytes = checked((height - 1) * bytesPerRow + rowBytes);
        if (data.Length < requiredBytes)
            throw new ArgumentException("Texture upload data is smaller than the requested upload region", nameof(data));

        var packedByteLength = checked(rowBytes * height);
        byte[] rented = null;
        ReadOnlySpan<byte> packedData;

        if (bytesPerRow == rowBytes)
            packedData = data[..packedByteLength];
        else
        {
            rented = ArrayPool<byte>.Shared.Rent(packedByteLength);
            var target = rented.AsSpan(0, packedByteLength);
            copyRowsToPacked(data[..requiredBytes], bytesPerRow, rowBytes, height, target);
            packedData = target;
        }

        try
        {
            backend.UploadTextureWithStagingBelt(TextureHandle, TextureFormat, packedData, width, height, x, y);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    static void copyRowsToPacked(ReadOnlySpan<byte> source,
        int sourceBytesPerRow,
        int rowBytes,
        int height,
        Span<byte> target)
    {
        if (sourceBytesPerRow == rowBytes && source.Length >= checked(rowBytes * height))
        {
            source[..checked(rowBytes * height)].CopyTo(target);
            return;
        }

        for (var row = 0; row < height; ++row)
            source.Slice(row * sourceBytesPerRow, rowBytes)
                .CopyTo(target.Slice(row * rowBytes, rowBytes));
    }

    static Rgba32 getUploadPixel(Color color, TextureOptions textureOptions)
    {
        if (!textureOptions.PreMultiply) return color.ToPixel<Rgba32>();

        var vec = color.ToScaledVector4();
        return Color.FromScaledVector(new(vec.X * vec.W, vec.Y * vec.W, vec.Z * vec.W, vec.W)).ToPixel<Rgba32>();
    }

    static SamplerDescriptor createSamplerDescriptor(TextureOptions options)
        => new()
        {
            MinFilter = toFilter(options.TextureMinFilter),
            MagFilter = toFilter(options.TextureMagFilter),
            MipmapFilter = toMipmapFilter(options.TextureMinFilter),
            AddressModeU = toAddressMode(options.TextureWrapS),
            AddressModeV = toAddressMode(options.TextureWrapT),
            AddressModeW = WGPUAddressMode.ClampToEdge,
            LodMaxClamp = float.MaxValue,
            MaxAnisotropy = 1
        };

    static nint getSamplerIdentity(TextureOptions options)
        => (int)options.TextureMinFilter |
            (int)options.TextureMagFilter << 4 |
            (int)options.TextureWrapS << 8 |
            (int)options.TextureWrapT << 12;

    static WGPUFilterMode toFilter(TextureFilter filter)
        => filter is TextureFilter.Nearest or TextureFilter.NearestMipmapNearest or TextureFilter.NearestMipmapLinear
            ? WGPUFilterMode.Nearest
            : WGPUFilterMode.Linear;

    static WGPUMipmapFilterMode toMipmapFilter(TextureFilter filter)
        => filter is TextureFilter.NearestMipmapNearest or TextureFilter.LinearMipmapNearest
            ? WGPUMipmapFilterMode.Nearest
            : WGPUMipmapFilterMode.Linear;

    static WGPUAddressMode toAddressMode(TextureWrap wrap)
        => wrap switch
        {
            TextureWrap.Repeat => WGPUAddressMode.Repeat,
            TextureWrap.MirroredRepeat => WGPUAddressMode.MirrorRepeat,
            _ => WGPUAddressMode.ClampToEdge
        };
}