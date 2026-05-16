namespace BrewLib.Graphics.Textures;

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Backend.OpenGL;
using IO;
using osuTK.Graphics.OpenGL;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Util;

public sealed class Texture2d : Texture2dRegion, IWritableTexture
{
    const int StackAllocThreshold = 1024;

    readonly IGraphicsBackend backend;

    int _textureId;

    nint fenceId;

    Texture2d(IGraphicsBackend backend, int textureId, int width, int height, nint fence = 0)
        : base(null, new(0, 0, width, height))
    {
        this.backend = backend ?? DrawState.Backend;
        _textureId = textureId;

        fenceId = fence == 0 ? GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0) : fence;
        if (fence == 0) GL.Flush();
    }

    public int TextureId
    {
        get
        {
            if (_textureId == 0) throw new InvalidOperationException("Texture not created");

            Wait(true);

            return _textureId;
        }
    }

    public IGraphicsBackend Backend => backend;
    public GraphicsResourceHandle NativeHandle => new("OpenGL", TextureId);

    static bool ClearTextureSupported => DrawState.Backend?.Capabilities.Has(GraphicsBackendFeatures.ClearTexture) ?? false;

    public void Update(Color color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposed, typeof(Texture2d));

        if (ClearTextureSupported)
        {
            var pix = color.ToPixel<Rgba32>();
            GL.ClearTexSubImage(_textureId,
                0,
                x,
                y,
                0,
                width,
                height,
                1,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ref pix);

            fenceId = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);

            return;
        }

        IMemoryOwner<Rgba32> spanOwner = null;
        scoped Span<Rgba32> span;

        var area = width * height;
        if (area <= StackAllocThreshold) span = stackalloc Rgba32[area];
        else
        {
            spanOwner = MemoryAllocator.Default.Allocate<Rgba32>(width * height);
            span = spanOwner.Memory.Span;
        }

        span.Fill(color.ToPixel<Rgba32>());

        DrawState.BindPrimaryTexture(_textureId);
        GL.TexSubImage2D(TextureTarget.Texture2D,
            0,
            x,
            y,
            width,
            height,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            ref MemoryMarshal.GetReference(span));

        DrawState.UnbindTexture(_textureId);
        fenceId = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);

        spanOwner?.Dispose();
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposed, typeof(Texture2d));

        DrawState.BindPrimaryTexture(_textureId);

        var buffer = bitmap.Frames.RootFrame.PixelBuffer;
        if (buffer.MemoryGroup.Count == 1)
            GL.TexSubImage2D(TextureTarget.Texture2D,
                0,
                x,
                y,
                buffer.Width,
                buffer.Height,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ref MemoryMarshal.GetReference(buffer.DangerousGetRowSpan(0)));
        else
            for (var i = 0; i < buffer.Height; ++i)
                GL.TexSubImage2D(TextureTarget.Texture2D,
                    0,
                    0,
                    y + i,
                    buffer.Width,
                    1,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ref MemoryMarshal.GetReference(buffer.DangerousGetRowSpan(i)));

        DrawState.UnbindTexture(_textureId);
        fenceId = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);
    }

    public static Image<Rgba32> LoadBitmap(string filename, ResourceContainer resourceContainer = null)
        => TextureLoader.LoadBitmap(filename, resourceContainer);

    public static TextureOptions LoadTextureOptions(string forBitmapFilename,
        ResourceContainer resourceContainer = null)
        => TextureLoader.LoadTextureOptions(forBitmapFilename, resourceContainer);

    public static Texture2d Load(string filename,
        ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null,
        IGraphicsBackend backend = null)
    {
        using var bitmap = LoadBitmap(filename, resourceContainer);
        return bitmap is not null ?
            Load(bitmap, textureOptions ?? LoadTextureOptions(filename, resourceContainer), backend) :
            null;
    }

    public static Texture2d Create(Color color,
        int width = 1,
        int height = 1,
        TextureOptions textureOptions = null,
        IGraphicsBackend backend = null)
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

        if (ClearTextureSupported)
        {
            GL.TexImage2D(TextureTarget.Texture2D,
                0,
                textureOptions.Srgb && DrawState.ColorCorrected ? PixelInternalFormat.Srgb8 : PixelInternalFormat.Rgba8,
                width,
                height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                0);

            var pix = color.ToPixel<Rgba32>();
            GL.ClearTexImage(textureId, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref pix);

            if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            OpenGlTextureOptions.ApplyParameters(textureOptions, TextureTarget.Texture2D);

            DrawState.UnbindTexture(textureId);
            return new(backend, textureId, width, height);
        }

        IMemoryOwner<Rgba32> spanOwner = null;
        scoped Span<Rgba32> span;

        var area = width * height;
        if (area <= StackAllocThreshold) span = stackalloc Rgba32[area];
        else
        {
            spanOwner = MemoryAllocator.Default.Allocate<Rgba32>(width * height);
            span = spanOwner.Memory.Span;
        }

        span.Fill(color.ToPixel<Rgba32>());

        GL.TexImage2D(TextureTarget.Texture2D,
            0,
            textureOptions.Srgb && DrawState.ColorCorrected ? PixelInternalFormat.Srgb8 : PixelInternalFormat.Rgba8,
            width,
            height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            ref MemoryMarshal.GetReference(span));

        spanOwner?.Dispose();

        if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        OpenGlTextureOptions.ApplyParameters(textureOptions, TextureTarget.Texture2D);

        DrawState.UnbindTexture(textureId);
        return new(backend, textureId, width, height);
    }

    public static Texture2d Load(Image<Rgba32> bitmap,
        TextureOptions textureOptions = null,
        IGraphicsBackend backend = null)
    {
        var width = int.Min(DrawState.MaxTextureSize, bitmap.Width);
        var height = int.Min(DrawState.MaxTextureSize, bitmap.Height);

        textureOptions ??= TextureOptions.Default;
        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;
        var compress = DrawState.UseTextureCompression;

        var format = sRgb ? compress ? PixelInternalFormat.CompressedSrgbS3tcDxt1Ext : PixelInternalFormat.Srgb8 :
            compress ? PixelInternalFormat.CompressedRgbaS3tcDxt5Ext : PixelInternalFormat.Rgba8;

        var textureId = GL.GenTexture();
        DrawState.BindTexture(textureId);

        var buffer = bitmap.Frames.RootFrame.PixelBuffer;
        if (buffer.MemoryGroup.Count == 1 && bitmap.Width <= width && bitmap.Height <= height)
            GL.TexImage2D(TextureTarget.Texture2D,
                0,
                format,
                width,
                height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ref MemoryMarshal.GetReference(buffer.DangerousGetRowSpan(0)));
        else
        {
            var pbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo);

            var dataSize = width * height * Unsafe.SizeOf<Rgba32>();
            GL.BufferData(BufferTarget.PixelUnpackBuffer, dataSize, 0, BufferUsageHint.StreamDraw);

            var mapped = GL.MapBuffer(BufferTarget.PixelUnpackBuffer, BufferAccess.WriteOnly)
                .AsSpan<Rgba32>(width * height);

            for (var i = 0; i < height; ++i) buffer.DangerousGetRowSpan(i)[..width].CopyTo(mapped[(i * width)..]);

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

            GL.DeleteBuffer(pbo);
        }

        if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        OpenGlTextureOptions.ApplyParameters(textureOptions, TextureTarget.Texture2D);

        DrawState.UnbindTexture(textureId);
        return new(backend, textureId, width, height);
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

    #region IDisposable Support

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                Free(this);
            }
            else Native.MainThreadScheduler(tex => Free((Texture2d)tex), this);
        }

        base.Dispose(disposing);
    }

    static void Free(Texture2d texture)
    {
        if (texture._textureId == 0) return;

        if (texture.fenceId != -1) GL.DeleteSync(texture.fenceId);

        DrawState.UnbindTexture(texture._textureId);
        GL.DeleteTexture(texture._textureId);
        texture._textureId = 0;
        texture.fenceId = -1;
    }

    #endregion
}
