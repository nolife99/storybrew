namespace BrewLib.Graphics.Textures;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IO;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Util;
using Image = SixLabors.ImageSharp.Image;

public sealed class Texture2d(int textureId, int width, int height, nint texFence) : Texture2dRegion(null,
    new(0, 0, width, height))
{
    static readonly bool useGlClearTex = GLFW.ExtensionSupported("GL_ARB_clear_texture");

    long bindlessId = -1;

    public long BindlessTextureHandle
    {
        get
        {
            if (bindlessId != -1) return bindlessId;

            GL.WaitSync(texFence, WaitSyncFlags.None, -1);
            GL.Arb.MakeTextureHandleResident(bindlessId = GL.Arb.GetTextureHandle(textureId));

            GL.DeleteSync(texFence);

            if (!BitConverter.IsLittleEndian)
                bindlessId = (long)(uint)(bindlessId & 0xFFFFFFFF) << 32 | (uint)(bindlessId >> 32 & 0xFFFFFFFF);

            return bindlessId;
        }
    }

    static Image<Rgba32> LoadBitmap(string filename, ResourceContainer resourceContainer = null)
    {
        using var stream = File.Exists(filename) ?
            File.OpenRead(filename) :
            resourceContainer?.GetStream(filename, ResourceSource.Embedded);

        if (stream is not null) return Image.Load<Rgba32>(stream);

        Trace.TraceWarning($"Texture not found: {filename}");
        return null;
    }

    public static TextureOptions LoadTextureOptions(string forBitmapFilename, ResourceContainer resourceContainer = null)
        => TextureOptions.Load(TextureOptions.GetOptionsFilename(forBitmapFilename), resourceContainer);

    public static Texture2d Load(string filename,
        ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null)
    {
        using var bitmap = LoadBitmap(filename, resourceContainer);
        return bitmap is not null ? Load(bitmap, textureOptions ?? LoadTextureOptions(filename, resourceContainer)) : null;
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

        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;

        var textureId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, textureId);
        GL.TexStorage2D(TextureTarget2d.Texture2D,
            1,
            sRgb ? SizedInternalFormat.Srgb8 : SizedInternalFormat.Rgba8,
            width,
            height);

        if (useGlClearTex) GL.ClearTexImage(textureId, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref color);
        else
        {
            using var spanOwner = Configuration.Default.MemoryAllocator.Allocate<Rgba32>(width * height);
            var span = spanOwner.Memory.Span;

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
        }

        if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        textureOptions.ApplyParameters(TextureTarget.Texture2D);

        return new(textureId, width, height, GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None));
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
        GL.TexStorage2D(TextureTarget2d.Texture2D,
            1,
            Unsafe.As<PixelInternalFormat, SizedInternalFormat>(ref format),
            width,
            height);

        var buffer = bitmap.Frames.RootFrame.PixelBuffer;
        if (buffer.MemoryGroup.Count == 1 && bitmap.Width <= width && bitmap.Height <= height)
            GL.TexSubImage2D(TextureTarget.Texture2D,
                0,
                0,
                0,
                width,
                height,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ref MemoryMarshal.GetReference(buffer.DangerousGetRowSpan(0)));
        else
        {
            var pbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pbo);

            var dataSize = width * height * Unsafe.SizeOf<Rgba32>();
            GL.BufferStorage(BufferTarget.PixelUnpackBuffer, dataSize, 0, BufferStorageFlags.MapWriteBit);

            ref var addr = ref Unsafe.AddByteOffset(ref Unsafe.NullRef<Rgba32>(),
                GL.MapBufferRange(BufferTarget.PixelUnpackBuffer,
                    0,
                    dataSize,
                    MapBufferAccessMask.MapWriteBit |
                    MapBufferAccessMask.MapInvalidateBufferBit |
                    MapBufferAccessMask.MapUnsynchronizedBit));

            for (var i = 0; i < height; ++i)
                buffer.DangerousGetRowSpan(i)[..width]
                    .CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref addr, i * width), width));

            GL.UnmapBuffer(BufferTarget.PixelUnpackBuffer);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, 0);

            GL.DeleteBuffer(pbo);
        }

        if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        textureOptions.ApplyParameters(TextureTarget.Texture2D);

        return new(textureId, width, height, GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None));
    }

    #region IDisposable Support

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            Native.MainThreadScheduler(() => GL.DeleteTexture(textureId)).Wait();
            if (disposing) disposed = true;
        }

        base.Dispose(disposing);
    }

    #endregion
}