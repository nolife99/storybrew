namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SDL3;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Textures;
using Util;

public sealed class OpenGlTextureFactory(IGraphicsBackend backend) : ITextureFactory
{
    public ITextureRegion Load(Image<Rgba32> bitmap, TextureOptions textureOptions = null)
        => OpenGlTexture.Load(backend, bitmap, textureOptions);

    public IWritableTexture Create(Color color,
        int width = 1,
        int height = 1,
        TextureOptions textureOptions = null)
        => OpenGlTexture.Create(backend, color, width, height, textureOptions);
}

public sealed class OpenGlAsyncTextureUploader(OpenGlGraphicsBackend backend)
    : AsyncTextureUploaderBase(() => backend.Capabilities.MaxTextureSize)
{
    protected override async ValueTask<PreparedTextureUpload> PrepareAsync(string source,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions,
        CancellationToken cancellationToken)
    {
        var description = CreateDescription(source, bitmap, textureOptions);
        var state = new CreateUploadState(description);
        OpenGlPreparedTextureUpload upload = null;

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
        => upload is OpenGlPreparedTextureUpload openGlUpload ?
            OpenGlTexture.Load(backend, openGlUpload) :
            throw new InvalidOperationException("OpenGL async texture uploader received an upload prepared by another backend");

    sealed class CreateUploadState(TextureUploadDescription description)
    {
        public OpenGlPreparedTextureUpload Upload { get; private set; }

        public void Run() => Upload = OpenGlPreparedTextureUpload.Create(description);
    }
}

sealed class OpenGlPreparedTextureUpload : PreparedTextureUpload
{
    const MapBufferAccessMask UploadMapFlags =
        MapBufferAccessMask.WriteBit |
        MapBufferAccessMask.InvalidateBufferBit;

    bool disposed;
    nint mapped;
    uint pixelUnpackBuffer;

    OpenGlPreparedTextureUpload(TextureUploadDescription description,
        uint pixelUnpackBuffer,
        nint mapped)
        : base(description)
    {
        this.pixelUnpackBuffer = pixelUnpackBuffer;
        this.mapped = mapped;
    }

    internal override Span<byte> WritableBytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (mapped == nint.Zero) throw new InvalidOperationException("OpenGL texture upload is not mapped");

            return mapped.AsSpan<byte>(ByteLength);
        }
    }

    internal static OpenGlPreparedTextureUpload Create(TextureUploadDescription description)
    {
        var pbo = OpenGlApi.GL.GenBuffer();
        OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, pbo);

        OpenGlApi.AllocateBuffer(BufferTargetARB.PixelUnpackBuffer,
            description.ByteLength,
            BufferUsageARB.StreamDraw);

        var mapped = OpenGlApi.MapBufferRange(BufferTargetARB.PixelUnpackBuffer,
            0,
            description.ByteLength,
            UploadMapFlags);
        OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);

        if (mapped != nint.Zero) return new(description, pbo, mapped);

        OpenGlApi.GL.DeleteBuffer(pbo);
        throw new InvalidOperationException($"Unable to map OpenGL texture upload PBO: {OpenGlApi.GL.GetError()}");
    }

    internal uint UnmapAndDetach()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var pbo = pixelUnpackBuffer;
        OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, pbo);
        if (mapped != nint.Zero)
        {
            OpenGlApi.GL.UnmapBuffer(BufferTargetARB.PixelUnpackBuffer);
            mapped = nint.Zero;
        }

        pixelUnpackBuffer = 0;
        return pbo;
    }

    public override void Dispose()
    {
        if (disposed) return;

        disposed = true;

        if (SDL.IsMainThread())
            releaseResources();
        else if (Native.MainThreadScheduler is not null)
            _ = Native.MainThreadScheduler(static s => ((OpenGlPreparedTextureUpload)s).releaseResources(), this)
                .AsTask();
        else
            releaseResources();
    }

    void releaseResources()
    {
        var pbo = pixelUnpackBuffer;
        if (pbo == 0) return;

        OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, pbo);
        if (mapped != nint.Zero)
        {
            OpenGlApi.GL.UnmapBuffer(BufferTargetARB.PixelUnpackBuffer);
            mapped = nint.Zero;
        }

        OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
        OpenGlApi.GL.DeleteBuffer(pbo);
        pixelUnpackBuffer = 0;
    }
}

sealed class OpenGlTexture : Texture2dRegion, IWritableTexture
{
    const MapBufferAccessMask UploadMapFlags =
        MapBufferAccessMask.WriteBit |
        MapBufferAccessMask.InvalidateBufferBit;

    nint fenceId = -1;

    int textureId;

    OpenGlTexture(IGraphicsBackend backend, int textureId, int width, int height, nint fence = 0)
        : base(null, new(0, 0, width, height))
    {
        Backend = backend ?? DrawState.Backend;
        this.textureId = textureId;

        if (fence == 0) setUploadFence(true);
        else fenceId = fence;
    }

    public int TextureId
    {
        get
        {
            if (textureId == 0) throw new InvalidOperationException("Texture not created");

            Wait(true);
            return textureId;
        }
    }

    static bool ClearTextureSupported
        => DrawState.Backend?.Capabilities.Has(GraphicsBackendFeatures.ClearTexture) ?? false;

    public IGraphicsBackend Backend { get; }

    public GraphicsResourceHandle NativeHandle => new(Backend.Name, TextureId);

    public void Update(Color color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (ClearTextureSupported)
        {
            var pixel = color.ToPixel<Rgba32>();
            OpenGlApi.GL.ClearTexSubImage((uint)textureId,
                0,
                x,
                y,
                0,
                (uint)width,
                (uint)height,
                1,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ref pixel);

            setUploadFence();
            return;
        }

        DrawState.BindPrimaryTexture(textureId);
        uploadTextureSubImageWithPbo(x, y, width, height, color.ToPixel<Rgba32>());
        DrawState.UnbindTexture(textureId);
        setUploadFence();
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        DrawState.BindPrimaryTexture(textureId);
        uploadTextureSubImageWithPbo(bitmap, x, y, bitmap.Width, bitmap.Height);
        DrawState.UnbindTexture(textureId);
        setUploadFence();
    }

    public static OpenGlTexture Create(IGraphicsBackend backend,
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

        var textureId = (int)OpenGlApi.GL.GenTexture();
        DrawState.BindTexture(textureId);

        var format = textureOptions.Srgb && DrawState.ColorCorrected ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8;
        if (ClearTextureSupported)
        {
            OpenGlApi.UploadTextureImage2D(TextureTarget.Texture2D,
                0,
                format,
                width,
                height,
                PixelFormat.Rgba,
                PixelType.UnsignedByte);

            var pixel = color.ToPixel<Rgba32>();
            OpenGlApi.GL.ClearTexImage((uint)textureId, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref pixel);
        }
        else
        {
            uploadTextureImageWithPbo(width, height, format, color.ToPixel<Rgba32>());
        }

        if (textureOptions.GenerateMipmaps) OpenGlApi.GL.GenerateMipmap(TextureTarget.Texture2D);
        OpenGlTextureOptions.ApplyParameters(textureOptions, TextureTarget.Texture2D);

        DrawState.UnbindTexture(textureId);
        return new(backend, textureId, width, height);
    }

    public static OpenGlTexture Load(IGraphicsBackend backend,
        Image<Rgba32> bitmap,
        TextureOptions textureOptions = null)
    {
        var width = int.Min(DrawState.MaxTextureSize, bitmap.Width);
        var height = int.Min(DrawState.MaxTextureSize, bitmap.Height);

        textureOptions ??= TextureOptions.Default;
        var textureId = (int)OpenGlApi.GL.GenTexture();
        DrawState.BindTexture(textureId);

        uploadTextureImageWithPbo(bitmap, width, height, getTextureFormat(textureOptions));

        if (textureOptions.GenerateMipmaps) OpenGlApi.GL.GenerateMipmap(TextureTarget.Texture2D);
        OpenGlTextureOptions.ApplyParameters(textureOptions, TextureTarget.Texture2D);

        DrawState.UnbindTexture(textureId);
        return new(backend, textureId, width, height);
    }

    internal static OpenGlTexture Load(IGraphicsBackend backend,
        OpenGlPreparedTextureUpload upload)
    {
        if (upload.Width < 1 || upload.Height < 1)
            throw new InvalidOperationException($"Invalid texture size: {upload.Width}x{upload.Height}");

        if (upload.Format != TextureUploadFormat.Rgba8)
            throw new NotSupportedException($"Unsupported OpenGL texture upload format: {upload.Format}");

        var textureOptions = upload.Options ?? TextureOptions.Default;
        var textureId = (int)OpenGlApi.GL.GenTexture();
        DrawState.BindTexture(textureId);

        try
        {
            uploadPreparedTextureImage(upload, getTextureFormat(textureOptions));

            if (textureOptions.GenerateMipmaps) OpenGlApi.GL.GenerateMipmap(TextureTarget.Texture2D);
            OpenGlTextureOptions.ApplyParameters(textureOptions, TextureTarget.Texture2D);

            DrawState.UnbindTexture(textureId);
            return new(backend, textureId, upload.Width, upload.Height);
        }
        catch
        {
            DrawState.UnbindTexture(textureId);
            OpenGlApi.GL.DeleteTexture((uint)textureId);
            throw;
        }
    }

    internal bool Wait(bool canBlock)
    {
        var fence = fenceId;
        if (fence == -1) return true;

        if (canBlock)
        {
            OpenGlApi.GL.WaitSync(fence, (uint)0, ulong.MaxValue);
            OpenGlApi.GL.DeleteSync(fence);
            fenceId = -1;
            return true;
        }

        OpenGlApi.GL.GetSync(fence, SyncParameterName.SyncStatus, sizeof(int), out _, out var values);
        if (values == (int)GLEnum.Unsignaled) return false;

        OpenGlApi.GL.DeleteSync(fence);
        fenceId = -1;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
                Free(this);
            else
                _ = Native.MainThreadScheduler(static texture => Free((OpenGlTexture)texture), this);
        }

        base.Dispose(disposing);
    }

    static void Free(OpenGlTexture texture)
    {
        if (texture.textureId == 0) return;

        texture.deleteFence();

        DrawState.UnbindTexture(texture.textureId);
        OpenGlApi.GL.DeleteTexture((uint)texture.textureId);
        texture.textureId = 0;
    }

    void setUploadFence(bool flush = false)
    {
        deleteFence();
        fenceId = OpenGlApi.GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, (uint)0);
        if (flush) OpenGlApi.GL.Flush();
    }

    void deleteFence()
    {
        if (fenceId == -1) return;

        OpenGlApi.GL.DeleteSync(fenceId);
        fenceId = -1;
    }

    static InternalFormat getTextureFormat(TextureOptions textureOptions)
    {
        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;
        return sRgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8;
    }

    static void uploadPreparedTextureImage(OpenGlPreparedTextureUpload upload,
        InternalFormat format)
    {
        var pbo = upload.UnmapAndDetach();
        try
        {
            OpenGlApi.UploadTextureImage2D(TextureTarget.Texture2D,
                0,
                format,
                upload.Width,
                upload.Height,
                PixelFormat.Rgba,
                PixelType.UnsignedByte);
        }
        finally
        {
            OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
            OpenGlApi.GL.DeleteBuffer(pbo);
        }
    }

    static void uploadTextureImageWithPbo(Image<Rgba32> bitmap,
        int width,
        int height,
        InternalFormat format)
    {
        var pbo = mapPixelUnpackBuffer(width, height, out var pixels);
        try
        {
            copyBitmapRows(bitmap, pixels, width, height);
            uploadMappedTextureImage(pbo, width, height, format);
        }
        catch
        {
            OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
            OpenGlApi.GL.DeleteBuffer(pbo);
            throw;
        }
    }

    static void uploadTextureImageWithPbo(int width,
        int height,
        InternalFormat format,
        Rgba32 pixel)
    {
        var pbo = mapPixelUnpackBuffer(width, height, out var pixels);
        try
        {
            pixels.Fill(pixel);
            uploadMappedTextureImage(pbo, width, height, format);
        }
        catch
        {
            OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
            OpenGlApi.GL.DeleteBuffer(pbo);
            throw;
        }
    }

    static void uploadTextureSubImageWithPbo(Image<Rgba32> bitmap,
        int x,
        int y,
        int width,
        int height)
    {
        var pbo = mapPixelUnpackBuffer(width, height, out var pixels);
        try
        {
            copyBitmapRows(bitmap, pixels, width, height);
            uploadMappedTextureSubImage(pbo, x, y, width, height);
        }
        catch
        {
            OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
            OpenGlApi.GL.DeleteBuffer(pbo);
            throw;
        }
    }

    static void uploadTextureSubImageWithPbo(int x,
        int y,
        int width,
        int height,
        Rgba32 pixel)
    {
        var pbo = mapPixelUnpackBuffer(width, height, out var pixels);
        try
        {
            pixels.Fill(pixel);
            uploadMappedTextureSubImage(pbo, x, y, width, height);
        }
        catch
        {
            OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
            OpenGlApi.GL.DeleteBuffer(pbo);
            throw;
        }
    }

    static uint mapPixelUnpackBuffer(int width, int height, out Span<Rgba32> pixels)
    {
        var pbo = OpenGlApi.GL.GenBuffer();
        OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, pbo);

        var pixelCount = checked(width * height);
        var byteCount = checked(pixelCount * Unsafe.SizeOf<Rgba32>());
        OpenGlApi.AllocateBuffer(BufferTargetARB.PixelUnpackBuffer, byteCount, BufferUsageARB.StreamDraw);

        var mapped = OpenGlApi.MapBufferRange(BufferTargetARB.PixelUnpackBuffer,
            0,
            byteCount,
            UploadMapFlags);
        if (mapped == nint.Zero)
        {
            OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
            OpenGlApi.GL.DeleteBuffer(pbo);
            throw new InvalidOperationException($"Unable to map OpenGL texture upload PBO: {OpenGlApi.GL.GetError()}");
        }

        pixels = mapped.AsSpan<Rgba32>(pixelCount);
        return pbo;
    }

    static void uploadMappedTextureImage(uint pbo,
        int width,
        int height,
        InternalFormat format)
    {
        OpenGlApi.GL.UnmapBuffer(BufferTargetARB.PixelUnpackBuffer);
        OpenGlApi.UploadTextureImage2D(TextureTarget.Texture2D,
            0,
            format,
            width,
            height,
            PixelFormat.Rgba,
            PixelType.UnsignedByte);

        OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
        OpenGlApi.GL.DeleteBuffer(pbo);
    }

    static void uploadMappedTextureSubImage(uint pbo,
        int x,
        int y,
        int width,
        int height)
    {
        OpenGlApi.GL.UnmapBuffer(BufferTargetARB.PixelUnpackBuffer);
        OpenGlApi.UploadTextureSubImage2D(TextureTarget.Texture2D,
            0,
            x,
            y,
            width,
            height,
            PixelFormat.Rgba,
            PixelType.UnsignedByte);

        OpenGlApi.GL.BindBuffer(BufferTargetARB.PixelUnpackBuffer, 0);
        OpenGlApi.GL.DeleteBuffer(pbo);
    }

    static void copyBitmapRows(Image<Rgba32> bitmap,
        Span<Rgba32> target,
        int width,
        int height)
    {
        var buffer = bitmap.Frames.RootFrame.PixelBuffer;
        for (var row = 0; row < height; ++row)
            buffer.DangerousGetRowSpan(row)[..width].CopyTo(target.Slice(row * width, width));
    }
}
