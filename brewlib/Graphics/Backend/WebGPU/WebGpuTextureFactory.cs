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
        catch
        {
            webGpuUpload.Dispose();
            throw;
        }
    }
}

sealed class WebGpuPreparedTextureUpload : PreparedTextureUpload
{
    IMemoryOwner<byte> bytes;
    bool disposed;

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

    readonly WebGpuGraphicsBackend backend;
    readonly nint samplerIdentity;
    readonly TextureOptions textureOptions;
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
        TextureHandle = texture;
        TextureViewHandle = textureView;
        SamplerHandle = sampler;
        this.textureOptions = textureOptions;
        samplerIdentity = getSamplerIdentity(textureOptions);
    }

    public WgpuTexture* TextureHandle { get; private set; }

    public TextureView* TextureViewHandle { get; private set; }

    public Sampler* SamplerHandle { get; private set; }

    public GraphicsResourceHandle SamplerIdentity => new(backend.Name, samplerIdentity);
    public IGraphicsBackend Backend => backend;
    public GraphicsResourceHandle NativeHandle => new(backend.Name, (nint)TextureHandle);

    public void Update(Color color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

        uploadColor(getUploadPixel(color, textureOptions), width, height, x, y);
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposedTexture, this);

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

        var textureOptions = upload.Options ?? TextureOptions.Default;
        var texture = createEmptyTexture(backend, upload.Width, upload.Height, textureOptions);

        try
        {
            texture.uploadPreparedBytes(upload.ReadableBytes, upload.Width, upload.Height, upload.BytesPerRow);
            backend.RetireDisposable(upload);
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
            backend.PurgeCachedBindGroupsReferencing(TextureViewHandle, SamplerHandle);

            if (TextureViewHandle is not null) backend.RetireTextureView(TextureViewHandle);
            if (SamplerHandle is not null) backend.RetireSampler(SamplerHandle);
            if (TextureHandle is not null) backend.RetireTexture(TextureHandle);

            TextureHandle = null;
            TextureViewHandle = null;
            SamplerHandle = null;
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

    void uploadColor(Rgba32 pixel, int width, int height, int x, int y)
    {
        if (width <= 0 || height <= 0) return;

        var rowBytes = checked(width * 4);
        var uploadBytesPerRow = align(rowBytes, TextureCopyBytesPerRowAlignment);
        var uploadByteLength = checked(uploadBytesPerRow * height);

        WgpuBuffer* uploadBuffer = null;
        var mapped = false;
        var uploadBufferRetired = false;
        try
        {
            uploadBuffer = createMappedUploadBuffer(uploadByteLength, out var uploadMemory);
            mapped = true;

            for (var row = 0; row < height; ++row)
                MemoryMarshal.Cast<byte, Rgba32>(uploadMemory.Slice(row * uploadBytesPerRow, rowBytes)).Fill(pixel);

            backend.Api.BufferUnmap(uploadBuffer);
            mapped = false;

            backend.RetireBuffer(uploadBuffer);
            uploadBufferRetired = true;
            submitCopyBufferToTexture(uploadBuffer,
                uploadBytesPerRow,
                width,
                height,
                x,
                y);
        }
        finally
        {
            if (uploadBuffer is not null && !uploadBufferRetired)
            {
                if (mapped)
                    backend.Api.BufferUnmap(uploadBuffer);

                backend.Api.BufferRelease(uploadBuffer);
            }
        }
    }

    void uploadBitmapRows(Image<Rgba32> bitmap, int width, int height, int x, int y)
    {
        if (width <= 0 || height <= 0) return;

        var rowBytes = checked(width * 4);
        var uploadBytesPerRow = align(rowBytes, TextureCopyBytesPerRowAlignment);
        var uploadByteLength = checked(uploadBytesPerRow * height);

        WgpuBuffer* uploadBuffer = null;
        var mapped = false;
        var uploadBufferRetired = false;
        try
        {
            uploadBuffer = createMappedUploadBuffer(uploadByteLength, out var uploadMemory);
            mapped = true;

            var source = bitmap.Frames.RootFrame.PixelBuffer;
            for (var row = 0; row < height; ++row)
            {
                var sourceRow = MemoryMarshal.AsBytes(source.DangerousGetRowSpan(row)[..width]);
                sourceRow.CopyTo(uploadMemory.Slice(row * uploadBytesPerRow, rowBytes));
            }

            backend.Api.BufferUnmap(uploadBuffer);
            mapped = false;

            backend.RetireBuffer(uploadBuffer);
            uploadBufferRetired = true;
            submitCopyBufferToTexture(uploadBuffer,
                uploadBytesPerRow,
                width,
                height,
                x,
                y);
        }
        finally
        {
            if (uploadBuffer is not null && !uploadBufferRetired)
            {
                if (mapped)
                    backend.Api.BufferUnmap(uploadBuffer);

                backend.Api.BufferRelease(uploadBuffer);
            }
        }
    }

    void uploadPreparedBytes(scoped ReadOnlySpan<byte> data, int width, int height, int bytesPerRow)
    {
        if (width <= 0 || height <= 0) return;

        bytesPerRow = bytesPerRow <= 0 ? checked(width * 4) : bytesPerRow;
        var rowBytes = checked(width * 4);
        var requiredBytes = checked((height - 1) * bytesPerRow + rowBytes);
        if (data.Length < requiredBytes)
            throw new ArgumentException("Texture upload data is smaller than the requested upload region", nameof(data));

        ImageCopyTexture destination = new()
        {
            Texture = TextureHandle,
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

        fixed (byte* source = data[..requiredBytes])
            backend.Api.QueueWriteTexture(backend.QueueHandle,
                in destination,
                source,
                (nuint)requiredBytes,
                in layout,
                in extent);
    }

    void uploadBytes(scoped ReadOnlySpan<byte> data, int width, int height, int x, int y, int bytesPerRow = 0)
    {
        if (width <= 0 || height <= 0) return;

        bytesPerRow = bytesPerRow <= 0 ? checked(width * 4) : bytesPerRow;
        var rowBytes = checked(width * 4);
        var requiredBytes = checked((height - 1) * bytesPerRow + rowBytes);
        if (data.Length < requiredBytes)
            throw new ArgumentException("Texture upload data is smaller than the requested upload region", nameof(data));

        var uploadBytesPerRow = align(rowBytes, TextureCopyBytesPerRowAlignment);
        var uploadByteLength = checked(uploadBytesPerRow * height);

        WgpuBuffer* uploadBuffer = null;
        var mapped = false;
        var uploadBufferRetired = false;
        try
        {
            uploadBuffer = createMappedUploadBuffer(uploadByteLength, out var uploadMemory);
            mapped = true;
            copyRowsToUploadBuffer(data[..requiredBytes], bytesPerRow, rowBytes, height, uploadMemory, uploadBytesPerRow);
            backend.Api.BufferUnmap(uploadBuffer);
            mapped = false;

            backend.RetireBuffer(uploadBuffer);
            uploadBufferRetired = true;
            submitCopyBufferToTexture(uploadBuffer,
                uploadBytesPerRow,
                width,
                height,
                x,
                y);
        }
        finally
        {
            if (uploadBuffer is not null && !uploadBufferRetired)
            {
                if (mapped)
                    backend.Api.BufferUnmap(uploadBuffer);

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
            Texture = TextureHandle,
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
            backend.SubmitCommandEncoder(encoder);
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

    static Rgba32 getUploadPixel(Color color, TextureOptions textureOptions)
    {
        if (!textureOptions.PreMultiply) return color.ToPixel<Rgba32>();

        var vec = color.ToScaledVector4();
        return Color.FromScaledVector(new(vec.X * vec.W, vec.Y * vec.W, vec.Z * vec.W, vec.W)).ToPixel<Rgba32>();
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
            (int)options.TextureMagFilter << 4 |
            (int)options.TextureWrapS << 8 |
            (int)options.TextureWrapT << 12;

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