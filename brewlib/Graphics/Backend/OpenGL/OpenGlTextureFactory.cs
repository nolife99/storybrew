namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using osuTK.Graphics.OpenGL;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

internal sealed class OpenGlPreparedTextureUpload : PreparedTextureUpload
{
    int pixelUnpackBuffer;
    nint mapped;
    bool disposed;

    OpenGlPreparedTextureUpload(TextureUploadDescription description,
        int pixelUnpackBuffer,
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
        var pbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo);

        GL.BufferData(BufferTarget.PixelUnpackBuffer, description.ByteLength, 0, BufferUsageHint.StreamDraw);

        var mapped = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, BufferAccess.WriteOnly);
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);

        if (mapped != nint.Zero) return new(description, pbo, mapped);

        GL.DeleteBuffer(pbo);
        throw new InvalidOperationException($"Unable to map OpenGL texture upload PBO: {GL.GetError()}");
    }

    internal int UnmapAndDetach()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var pbo = pixelUnpackBuffer;
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo);
        if (mapped != nint.Zero)
        {
            GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer);
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

        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo);
        if (mapped != nint.Zero)
        {
            GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer);
            mapped = nint.Zero;
        }

        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
        GL.DeleteBuffer(pbo);
        pixelUnpackBuffer = 0;
    }
}

internal sealed class OpenGlTexture : Texture2dRegion, IWritableTexture
{
    readonly IGraphicsBackend backend;

    int textureId;
    nint fenceId = -1;

    OpenGlTexture(IGraphicsBackend backend, int textureId, int width, int height, nint fence = 0)
        : base(null, new(0, 0, width, height))
    {
        this.backend = backend ?? DrawState.Backend;
        this.textureId = textureId;

        if (fence == 0) setUploadFence(flush: true);
        else fenceId = fence;
    }

    public IGraphicsBackend Backend => backend;
    public GraphicsResourceHandle NativeHandle => new(backend.Name, TextureId);

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

        var textureId = GL.GenTexture();
        DrawState.BindTexture(textureId);

        var format = textureOptions.Srgb && DrawState.ColorCorrected ? PixelInternalFormat.Srgb8 : PixelInternalFormat.Rgba8;
        if (ClearTextureSupported)
        {
            GL.TexImage2D(TextureTarget.Texture2D,
                0,
                format,
                width,
                height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                0);

            var pixel = color.ToPixel<Rgba32>();
            GL.ClearTexImage(textureId, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref pixel);
        }
        else
        {
            uploadTextureImageWithPbo(width, height, format, color.ToPixel<Rgba32>());
        }

        if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
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
        var textureId = GL.GenTexture();
        DrawState.BindTexture(textureId);

        uploadTextureImageWithPbo(bitmap, width, height, getTextureFormat(textureOptions));

        if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
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
        var textureId = GL.GenTexture();
        DrawState.BindTexture(textureId);

        try
        {
            uploadPreparedTextureImage(upload, getTextureFormat(textureOptions));

            if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            OpenGlTextureOptions.ApplyParameters(textureOptions, TextureTarget.Texture2D);

            DrawState.UnbindTexture(textureId);
            return new(backend, textureId, upload.Width, upload.Height);
        }
        catch
        {
            DrawState.UnbindTexture(textureId);
            GL.DeleteTexture(textureId);
            throw;
        }
    }

    public void Update(Color color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (ClearTextureSupported)
        {
            var pixel = color.ToPixel<Rgba32>();
            GL.ClearTexSubImage(textureId,
                0,
                x,
                y,
                0,
                width,
                height,
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

    internal bool Wait(bool canBlock)
    {
        var fence = fenceId;
        if (fence == -1) return true;

        if (canBlock)
        {
            GL.WaitSync(fence, WaitSyncFlags.None, ulong.MaxValue);
            GL.DeleteSync(fence);
            fenceId = -1;
            return true;
        }

        GL.GetSync(fence, SyncParameterName.SyncStatus, sizeof(int), out _, out var values);
        if (values == 0x9118) return false;

        GL.DeleteSync(fence);
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
        GL.DeleteTexture(texture.textureId);
        texture.textureId = 0;
    }

    void setUploadFence(bool flush = false)
    {
        deleteFence();
        fenceId = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
        if (flush) GL.Flush();
    }

    void deleteFence()
    {
        if (fenceId == -1) return;

        GL.DeleteSync(fenceId);
        fenceId = -1;
    }

    static PixelInternalFormat getTextureFormat(TextureOptions textureOptions)
    {
        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;
        var compress = DrawState.UseTextureCompression;

        return sRgb ?
            compress ? PixelInternalFormat.CompressedSrgbS3tcDxt1Ext : PixelInternalFormat.Srgb8 :
            compress ? PixelInternalFormat.CompressedRgbaS3tcDxt5Ext : PixelInternalFormat.Rgba8;
    }

    static void uploadPreparedTextureImage(OpenGlPreparedTextureUpload upload,
        PixelInternalFormat format)
    {
        var pbo = upload.UnmapAndDetach();
        try
        {
            GL.TexImage2D(TextureTarget.Texture2D,
                0,
                format,
                upload.Width,
                upload.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                0);
        }
        finally
        {
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            GL.DeleteBuffer(pbo);
        }
    }

    static void uploadTextureImageWithPbo(Image<Rgba32> bitmap,
        int width,
        int height,
        PixelInternalFormat format)
    {
        var pbo = mapPixelUnpackBuffer(width, height, out var pixels);
        try
        {
            copyBitmapRows(bitmap, pixels, width, height);
            uploadMappedTextureImage(pbo, width, height, format);
        }
        catch
        {
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            GL.DeleteBuffer(pbo);
            throw;
        }
    }

    static void uploadTextureImageWithPbo(int width,
        int height,
        PixelInternalFormat format,
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
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            GL.DeleteBuffer(pbo);
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
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            GL.DeleteBuffer(pbo);
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
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            GL.DeleteBuffer(pbo);
            throw;
        }
    }

    static int mapPixelUnpackBuffer(int width, int height, out Span<Rgba32> pixels)
    {
        var pbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo);

        var pixelCount = checked(width * height);
        var byteCount = checked(pixelCount * Unsafe.SizeOf<Rgba32>());
        GL.BufferData(BufferTarget.PixelUnpackBuffer, byteCount, 0, BufferUsageHint.StreamDraw);

        var mapped = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, BufferAccess.WriteOnly);
        if (mapped == nint.Zero)
        {
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
            GL.DeleteBuffer(pbo);
            throw new InvalidOperationException($"Unable to map OpenGL texture upload PBO: {GL.GetError()}");
        }

        pixels = mapped.AsSpan<Rgba32>(pixelCount);
        return pbo;
    }

    static void uploadMappedTextureImage(int pbo,
        int width,
        int height,
        PixelInternalFormat format)
    {
        GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer);
        GL.TexImage2D(TextureTarget.Texture2D,
            0,
            format,
            width,
            height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            0);

        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
        GL.DeleteBuffer(pbo);
    }

    static void uploadMappedTextureSubImage(int pbo,
        int x,
        int y,
        int width,
        int height)
    {
        GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer);
        GL.TexSubImage2D(TextureTarget.Texture2D,
            0,
            x,
            y,
            width,
            height,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            0);

        GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
        GL.DeleteBuffer(pbo);
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
