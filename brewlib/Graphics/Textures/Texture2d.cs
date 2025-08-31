namespace BrewLib.Graphics.Textures;

using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.IO;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

public sealed class Texture2d : Texture2dRegion
{
    const int StackAllocThreshold = 1024;

    public static readonly bool BindlessTexturesSupported = DrawState.Extensions.Contains("GL_ARB_bindless_texture");
    int _textureId;

    long bindlessId = -1;

    nint fenceId;

    Texture2d(int textureId, int width, int height) : base(null, new(0, 0, width, height))
    {
        _textureId = textureId;
        fenceId = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, 0);

        GL.Flush();
    }

    public int TextureId
    {
        get
        {
            if (_textureId == 0) throw new InvalidOperationException("Texture not created");

            ObjectDisposedException.ThrowIf(disposed, typeof(Texture2d));

            Wait(true);

            return _textureId;
        }
    }

    public long BindlessTextureHandle
    {
        get
        {
            if (bindlessId != -1) return bindlessId;

            if (!BindlessTexturesSupported) throw new InvalidOperationException("Bindless textures not supported");

            GL.Arb.MakeTextureHandleResident(bindlessId = GL.Arb.GetTextureHandle(TextureId));
            if (!BitConverter.IsLittleEndian)
                bindlessId = (long)(uint)(bindlessId & 0xFFFFFFFF) << 32 | (uint)(bindlessId >> 32 & 0xFFFFFFFF);

            return bindlessId;
        }
    }

    public void Update(Rgba32 color, int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(disposed, typeof(Texture2d));

        if (DrawState.Extensions.Contains("GL_ARB_clear_texture"))
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
                ref color);
        else
        {
            IMemoryOwner<Rgba32> spanOwner = null;
            scoped Span<Rgba32> span;

            var area = width * height;
            if (area <= StackAllocThreshold) span = stackalloc Rgba32[area];
            else
            {
                spanOwner = MemoryAllocator.Default.Allocate<Rgba32>(width * height);
                span = spanOwner.Memory.Span;
            }

            span.Fill(color);

            GL.BindTexture(TextureTarget.Texture2D, _textureId);
            GL.TexSubImage2D(TextureTarget.Texture2D,
                0,
                x,
                y,
                width,
                height,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ref MemoryMarshal.GetReference(span));

            GL.BindTexture(TextureTarget.Texture2D, 0);
            spanOwner?.Dispose();
        }
    }

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        ObjectDisposedException.ThrowIf(disposed, typeof(Texture2d));

        GL.BindTexture(TextureTarget.Texture2D, _textureId);

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

        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public static Image<Rgba32> LoadBitmap(string filename, ResourceContainer resourceContainer = null)
    {
        using var stream = File.Exists(filename) ?
            File.OpenRead(filename) :
            resourceContainer?.GetStream(filename, ResourceSource.Embedded);

        if (stream is not null) return Image.Load<Rgba32>(stream);

        SDL.LogWarn(SDL.LogCategory.Video, $"Texture not found: {filename}");
        return null;
    }

    public static TextureOptions LoadTextureOptions(string forBitmapFilename,
        ResourceContainer resourceContainer = null)
        => TextureOptions.Load(TextureOptions.GetOptionsFilename(forBitmapFilename), resourceContainer);

    public static Texture2d Load(string filename,
        ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null)
    {
        using var bitmap = LoadBitmap(filename, resourceContainer);
        return bitmap is not null ?
            Load(bitmap, textureOptions ?? LoadTextureOptions(filename, resourceContainer)) :
            null;
    }

    public static Texture2d Create(Rgba32 color, int width = 1, int height = 1, TextureOptions textureOptions = null)
    {
        if (width < 1 || height < 1) throw new InvalidOperationException($"Invalid texture size: {width}x{height}");

        textureOptions ??= TextureOptions.Default;
        if (textureOptions.PreMultiply)
        {
            var ratio = color.A / 255f;
            color = new((byte)(color.R * ratio), (byte)(color.G * ratio), (byte)(color.B * ratio), color.A);
        }

        var textureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, textureId);
        GL.TexStorage2D(TextureTarget2d.Texture2D,
            1,
            textureOptions.Srgb && DrawState.ColorCorrected ? SizedInternalFormat.Srgb8 : SizedInternalFormat.Rgba8,
            width,
            height);

        if (DrawState.Extensions.Contains("GL_ARB_clear_texture"))
            GL.ClearTexImage(textureId, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref color);
        else
        {
            IMemoryOwner<Rgba32> spanOwner = null;
            scoped Span<Rgba32> span;

            var area = width * height;
            if (area <= StackAllocThreshold) span = stackalloc Rgba32[area];
            else
            {
                spanOwner = MemoryAllocator.Default.Allocate<Rgba32>(width * height);
                span = spanOwner.Memory.Span;
            }

            span.Fill(color);
            GL.TexSubImage2D(TextureTarget.Texture2D,
                0,
                0,
                0,
                width,
                height,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ref MemoryMarshal.GetReference(span));

            spanOwner?.Dispose();
        }

        if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        textureOptions.ApplyParameters(TextureTarget.Texture2D);

        GL.BindTexture(TextureTarget.Texture2D, 0);
        return new(textureId, width, height);
    }

    public static Texture2d Load(Image<Rgba32> bitmap, TextureOptions textureOptions = null)
    {
        var width = int.Min(DrawState.MaxTextureSize, bitmap.Width);
        var height = int.Min(DrawState.MaxTextureSize, bitmap.Height);

        textureOptions ??= TextureOptions.Default;
        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;
        var compress = DrawState.UseTextureCompression;

        var format = sRgb ? compress ? PixelInternalFormat.CompressedSrgbS3tcDxt1Ext : PixelInternalFormat.Srgb8 :
            compress ? PixelInternalFormat.CompressedRgbaS3tcDxt5Ext : PixelInternalFormat.Rgba8;

        var textureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, textureId);

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
            GL.BufferStorage(BufferTarget.PixelUnpackBuffer, dataSize, 0, BufferStorageFlags.MapWriteBit);

            ref var addr = ref GL.MapBufferRange(BufferTarget.PixelUnpackBuffer,
                    0,
                    dataSize,
                    MapBufferAccessMask.MapWriteBit | MapBufferAccessMask.MapInvalidateBufferBit |
                    MapBufferAccessMask.MapUnsynchronizedBit)
                .AsRef<Rgba32>();

            for (var i = 0; i < height; ++i)
                buffer.DangerousGetRowSpan(i)[..width]
                    .CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref addr, i * width), width));

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
        textureOptions.ApplyParameters(TextureTarget.Texture2D);

        GL.BindTexture(TextureTarget.Texture2D, 0);
        return new(textureId, width, height);
    }

    internal bool Wait(bool canBlock)
    {
        var fence = fenceId;
        if (fence == -1) return true;

        if (canBlock)
        {
            GL.ClientWaitSync(fence, ClientWaitSyncFlags.None, ulong.MaxValue);
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

                _textureId = 0;
                bindlessId = -1;
            }
            else Native.MainThreadScheduler(tex => Free((Texture2d)tex), this);
        }

        base.Dispose(disposing);
    }

    static void Free(Texture2d texture)
    {
        GL.DeleteTexture(texture._textureId);
        if (texture.fenceId != -1) GL.DeleteSync(texture.fenceId);
    }

    #endregion
}