namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Silk.NET.WebGPU;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Textures;
using Color = SixLabors.ImageSharp.Color;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;
using WgpuBufferUsage = Silk.NET.WebGPU.BufferUsage;
using WgpuTexture = Silk.NET.WebGPU.Texture;

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
        WebGpuPreparedTextureUpload upload = null;

        try
        {
            upload = WebGpuPreparedTextureUpload.Create(description);
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
    {
        if (upload is not WebGpuPreparedTextureUpload webGpuUpload)
            throw new InvalidOperationException("WebGPU async texture uploader received an upload prepared by another backend");

        try
        {
            return WebGpuTexture.Load(backend, webGpuUpload);
        }
        finally
        {
            webGpuUpload.Dispose();
        }
    }
}

sealed class WebGpuPreparedTextureUpload : PreparedTextureUpload
{
    bool disposed;
    IMemoryOwner<byte> bytes;

    WebGpuPreparedTextureUpload(TextureUploadDescription description, IMemoryOwner<byte> bytes)
        : base(description)
        => this.bytes = bytes;

    internal override Span<byte> WritableBytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return bytes.Memory.Span[..ByteLength];
        }
    }

    internal ReadOnlySpan<byte> ReadableBytes => WritableBytes;

    internal static WebGpuPreparedTextureUpload Create(TextureUploadDescription description)
        => new(description, MemoryAllocator.Default.Allocate<byte>(description.ByteLength));

    public override void Dispose()
    {
        if (disposed) return;

        disposed = true;
        bytes.Dispose();
        bytes = null;
    }
}

public unsafe sealed class WebGpuTexture : Texture2dRegion, IWritableTexture, ITextureSamplerIdentity
{
    const int TextureCopyBytesPerRowAlignment = 256;
    const int StagedTextureUploadThresholdBytes = 256 * 1024;

    readonly WebGpuGraphicsBackend backend;
    readonly TextureOptions textureOptions;
    readonly nint samplerIdentity;
    WgpuTexture* texture;
    TextureView* textureView;
    Sampler* sampler;
    bool disposedTexture;

    WebGpuTexture(WebGpuGraphicsBackend backend,
        WgpuTexture* texture,
        TextureView* textureView,
        Sampler* sampler,
        int width,
        int height,
        TextureOptions textureOptions)
        : base(null, new(0, 0, width, height))
    {
        this.backend = backend;
        this.texture = texture;
        this.textureView = textureView;
        this.sampler = sampler;
        this.textureOptions = textureOptions;
        samplerIdentity = getSamplerIdentity(textureOptions);
    }

    public WgpuTexture* TextureHandle => texture;
    public TextureView* TextureViewHandle => textureView;
    public Sampler* SamplerHandle => sampler;
    public IGraphicsBackend Backend => backend;
    public GraphicsResourceHandle NativeHandle => new(backend.Name, (nint)texture);
    public GraphicsResourceHandle SamplerIdentity => new(backend.Name, samplerIdentity);

    public void Update(Color color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

        var pixel = color.ToPixel<Rgba32>();
        if (textureOptions.PreMultiply)
        {
            var vec = color.ToScaledVector4();
            pixel = Color.FromScaledVector(new(vec.X * vec.W, vec.Y * vec.W, vec.Z * vec.W, vec.W)).ToPixel<Rgba32>();
        }

        var byteLength = checked(width * height * 4);
        using var data = MemoryAllocator.Default.Allocate<byte>(byteLength);
        var bytes = data.Memory.Span[..byteLength];
        MemoryMarshal.Cast<byte, Rgba32>(bytes).Fill(pixel);
        uploadBytes(bytes, width, height, x, y);
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

        var width = int.Min(bitmap.Width, Width - x);
        var height = int.Min(bitmap.Height, Height - y);
        using var data = copyBitmapRows(bitmap, width, height);
        uploadBytes(data.Memory.Span[..checked(width * height * 4)], width, height, x, y);
    }

    public static WebGpuTexture Create(WebGpuGraphicsBackend backend,
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
            color = Color.FromScaledVector(new(vec.X * vec.W, vec.Y * vec.W, vec.Z * vec.W, vec.W));
        }

        var texture = createEmptyTexture(backend, width, height, textureOptions);
        var byteLength = checked(width * height * 4);
        using var data = MemoryAllocator.Default.Allocate<byte>(byteLength);
        var bytes = data.Memory.Span[..byteLength];
        MemoryMarshal.Cast<byte, Rgba32>(bytes).Fill(color.ToPixel<Rgba32>());
        texture.uploadBytes(bytes, width, height, 0, 0);
        return texture;
    }

    public static WebGpuTexture Load(WebGpuGraphicsBackend backend,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions = null)
    {
        var width = int.Min(backend.Capabilities.MaxTextureSize, bitmap.Width);
        var height = int.Min(backend.Capabilities.MaxTextureSize, bitmap.Height);

        textureOptions ??= TextureOptions.Default;
        var texture = createEmptyTexture(backend, width, height, textureOptions);

        try
        {
            using var data = copyBitmapRows(bitmap, width, height);
            texture.uploadBytes(data.Memory.Span[..checked(width * height * 4)], width, height, 0, 0);
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

        var textureOptions = upload.Options ?? TextureOptions.Default;
        var texture = createEmptyTexture(backend, upload.Width, upload.Height, textureOptions);

        try
        {
            texture.uploadBytes(upload.ReadableBytes, upload.Width, upload.Height, 0, 0, upload.BytesPerRow);
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
            backend.PurgeCachedBindGroupsReferencing(textureView, sampler);

            if (textureView is not null) backend.RetireTextureView(textureView);
            if (sampler is not null) backend.RetireSampler(sampler);
            if (texture is not null) backend.RetireTexture(texture);

            texture = null;
            textureView = null;
            sampler = null;
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
            Dimension = TextureDimension.Dimension2D,
            Size = new()
            {
                Width = (uint)width,
                Height = (uint)height,
                DepthOrArrayLayers = 1
            },
            Format = textureOptions.Srgb && DrawState.ColorCorrected
                ? TextureFormat.Rgba8UnormSrgb
                : TextureFormat.Rgba8Unorm,
            MipLevelCount = 1,
            SampleCount = 1
        };

        var texture = backend.Api.DeviceCreateTexture(backend.DeviceHandle, in textureDescriptor);
        if (texture is null)
            throw new InvalidOperationException("Unable to create WebGPU texture");

        TextureViewDescriptor viewDescriptor = new()
        {
            Format = textureDescriptor.Format,
            Dimension = TextureViewDimension.Dimension2D,
            MipLevelCount = 1,
            ArrayLayerCount = 1,
            Aspect = TextureAspect.All
        };

        var view = backend.Api.TextureCreateView(texture, in viewDescriptor);
        if (view is null)
        {
            backend.Api.TextureRelease(texture);
            throw new InvalidOperationException("Unable to create WebGPU texture view");
        }

        var samplerDescriptor = createSamplerDescriptor(textureOptions);
        var sampler = backend.Api.DeviceCreateSampler(backend.DeviceHandle, in samplerDescriptor);
        if (sampler is null)
        {
            backend.Api.TextureViewRelease(view);
            backend.Api.TextureRelease(texture);
            throw new InvalidOperationException("Unable to create WebGPU sampler");
        }

        return new(backend, texture, view, sampler, width, height, textureOptions);
    }

    void uploadBytes(ReadOnlySpan<byte> data, int width, int height, int x, int y, int bytesPerRow = 0)
    {
        if (width <= 0 || height <= 0) return;
        bytesPerRow = bytesPerRow <= 0 ? checked(width * 4) : bytesPerRow;
        var rowBytes = checked(width * 4);
        var requiredBytes = checked((height - 1) * bytesPerRow + rowBytes);
        if (data.Length < requiredBytes)
            throw new ArgumentException("Texture upload data is smaller than the requested upload region", nameof(data));

        data = data[..requiredBytes];
        if (requiredBytes >= StagedTextureUploadThresholdBytes)
        {
            uploadBytesWithTemporaryBuffer(data, width, height, x, y, bytesPerRow, rowBytes);
            return;
        }

        ImageCopyTexture destination = new()
        {
            Texture = texture,
            Origin = new()
            {
                X = (uint)x,
                Y = (uint)y
            },
            Aspect = TextureAspect.All
        };

        TextureDataLayout layout = new()
        {
            BytesPerRow = (uint)bytesPerRow,
            RowsPerImage = (uint)height
        };

        Extent3D extent = new()
        {
            Width = (uint)width,
            Height = (uint)height,
            DepthOrArrayLayers = 1
        };

        fixed (byte* source = data)
            backend.Api.QueueWriteTexture(backend.QueueHandle,
                in destination,
                source,
                (nuint)data.Length,
                in layout,
                in extent);
    }

    void uploadBytesWithTemporaryBuffer(ReadOnlySpan<byte> data,
        int width,
        int height,
        int x,
        int y,
        int sourceBytesPerRow,
        int rowBytes)
    {
        var uploadBytesPerRow = align(rowBytes, TextureCopyBytesPerRowAlignment);
        var uploadByteLength = checked(uploadBytesPerRow * height);

        WgpuBuffer* uploadBuffer = null;
        var mapped = false;
        try
        {
            uploadBuffer = createMappedUploadBuffer(uploadByteLength, out var uploadMemory);
            mapped = true;
            copyRowsToUploadBuffer(data, sourceBytesPerRow, rowBytes, height, uploadMemory, uploadBytesPerRow);
            backend.Api.BufferUnmap(uploadBuffer);
            mapped = false;

            submitCopyBufferToTexture(uploadBuffer,
                uploadBytesPerRow,
                width,
                height,
                x,
                y);
        }
        finally
        {
            if (uploadBuffer is not null)
            {
                if (mapped)
                    backend.Api.BufferUnmap(uploadBuffer);

                backend.Api.BufferDestroy(uploadBuffer);
                backend.Api.BufferRelease(uploadBuffer);
            }
        }
    }

    WgpuBuffer* createMappedUploadBuffer(int uploadByteLength, out Span<byte> uploadMemory)
    {
        BufferDescriptor descriptor = new()
        {
            Usage = WgpuBufferUsage.CopySrc,
            Size = (ulong)uploadByteLength,
            MappedAtCreation = true
        };

        var buffer = backend.Api.DeviceCreateBuffer(backend.DeviceHandle, in descriptor);
        if (buffer is null)
            throw new InvalidOperationException("Unable to create WebGPU texture upload buffer");

        var mapped = backend.Api.BufferGetMappedRange(buffer, 0, (nuint)uploadByteLength);
        if (mapped is null)
        {
            backend.Api.BufferDestroy(buffer);
            backend.Api.BufferRelease(buffer);
            throw new InvalidOperationException("Unable to map WebGPU texture upload buffer");
        }

        uploadMemory = new(mapped, uploadByteLength);
        return buffer;
    }

    void submitCopyBufferToTexture(WgpuBuffer* uploadBuffer,
        int uploadBytesPerRow,
        int width,
        int height,
        int x,
        int y)
    {
        ImageCopyBuffer source = new()
        {
            Buffer = uploadBuffer,
            Layout = new()
            {
                BytesPerRow = (uint)uploadBytesPerRow,
                RowsPerImage = (uint)height
            }
        };

        ImageCopyTexture destination = new()
        {
            Texture = texture,
            Origin = new()
            {
                X = (uint)x,
                Y = (uint)y
            },
            Aspect = TextureAspect.All
        };

        Extent3D extent = new()
        {
            Width = (uint)width,
            Height = (uint)height,
            DepthOrArrayLayers = 1
        };

        var encoder = backend.Api.DeviceCreateCommandEncoder(backend.DeviceHandle, null);
        if (encoder is null)
            throw new InvalidOperationException("Unable to create WebGPU texture upload command encoder");

        var submitOwnsEncoder = false;
        try
        {
            backend.Api.CommandEncoderCopyBufferToTexture(encoder,
                in source,
                in destination,
                in extent);

            submitOwnsEncoder = true;
            backend.SubmitImmediateCommandEncoder(encoder);
        }
        finally
        {
            if (!submitOwnsEncoder)
                backend.Api.CommandEncoderRelease(encoder);
        }
    }

    static void copyRowsToUploadBuffer(ReadOnlySpan<byte> source,
        int sourceBytesPerRow,
        int rowBytes,
        int height,
        Span<byte> target,
        int targetBytesPerRow)
    {
        if (sourceBytesPerRow == targetBytesPerRow &&
            source.Length >= checked(targetBytesPerRow * height))
        {
            source[..checked(targetBytesPerRow * height)].CopyTo(target);
            return;
        }

        for (var row = 0; row < height; ++row)
            source.Slice(row * sourceBytesPerRow, rowBytes)
                .CopyTo(target.Slice(row * targetBytesPerRow, rowBytes));
    }

    static IMemoryOwner<byte> copyBitmapRows(Image<Rgba32> bitmap, int width, int height)
    {
        var byteLength = checked(width * height * 4);
        IMemoryOwner<byte> data = null;

        try
        {
            data = MemoryAllocator.Default.Allocate<byte>(byteLength);
            var target = data.Memory.Span[..byteLength];
            var source = bitmap.Frames.RootFrame.PixelBuffer;
            var rowBytes = checked(width * 4);

            for (var row = 0; row < height; ++row)
            {
                var sourceRow = MemoryMarshal.AsBytes(source.DangerousGetRowSpan(row)[..width]);
                sourceRow.CopyTo(target.Slice(row * rowBytes, rowBytes));
            }

            return data;
        }
        catch
        {
            data?.Dispose();
            throw;
        }
    }

    static int align(int value, int alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : checked(value + alignment - remainder);
    }

    static SamplerDescriptor createSamplerDescriptor(TextureOptions options)
        => new()
        {
            MinFilter = toFilter(options.TextureMinFilter),
            MagFilter = toFilter(options.TextureMagFilter),
            MipmapFilter = toMipmapFilter(options.TextureMinFilter),
            AddressModeU = toAddressMode(options.TextureWrapS),
            AddressModeV = toAddressMode(options.TextureWrapT),
            AddressModeW = AddressMode.ClampToEdge,
            LodMaxClamp = float.MaxValue,
            MaxAnisotropy = 1
        };

    static nint getSamplerIdentity(TextureOptions options)
        => (int)options.TextureMinFilter |
            ((int)options.TextureMagFilter << 4) |
            ((int)options.TextureWrapS << 8) |
            ((int)options.TextureWrapT << 12);

    static FilterMode toFilter(TextureFilter filter)
        => filter is TextureFilter.Nearest or TextureFilter.NearestMipmapNearest or TextureFilter.NearestMipmapLinear
            ? FilterMode.Nearest
            : FilterMode.Linear;

    static MipmapFilterMode toMipmapFilter(TextureFilter filter)
        => filter is TextureFilter.NearestMipmapNearest or TextureFilter.LinearMipmapNearest
            ? MipmapFilterMode.Nearest
            : MipmapFilterMode.Linear;

    static AddressMode toAddressMode(TextureWrap wrap)
        => wrap switch
        {
            TextureWrap.Repeat => AddressMode.Repeat,
            TextureWrap.MirroredRepeat => AddressMode.MirrorRepeat,
            _ => AddressMode.ClampToEdge
        };
}
