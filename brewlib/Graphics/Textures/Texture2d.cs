namespace BrewLib.Graphics.Textures;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Data;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.GraphicsLibraryFramework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Util;
using Image = SixLabors.ImageSharp.Image;

public sealed class Texture2d(int textureId, int width, int height, string description)
    : Texture2dRegion(null, new(0, 0, width, height)), BindableTexture
{
    public static readonly DecoderOptions ContiguousBufferDecoderOptions = loadDecoderOptions();
    static readonly Lazy<bool> useGlClearTex = new(() => GLFW.ExtensionSupported("GL_ARB_clear_texture"), false);

    public int TextureId => disposed ? throw new ObjectDisposedException(description) : textureId;

    public void Update(Image<Rgba32> bitmap, int x, int y, TextureOptions textureOptions) => GL.TextureSubImage2D(textureId, 0, x,
        y, bitmap.Width, bitmap.Height, PixelFormat.Rgba, PixelType.UnsignedByte,
        ref MemoryMarshal.GetReference(bitmap.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)));

    public static Image<Rgba32> LoadBitmap(string filename, ResourceContainer resourceContainer = null)
    {
        if (File.Exists(filename))
            using (var stream = File.OpenRead(filename))
                return Image.Load<Rgba32>(ContiguousBufferDecoderOptions, stream);

        using (var stream = resourceContainer?.GetStream(filename, ResourceSource.Embedded))
            if (stream is not null)
                return Image.Load<Rgba32>(ContiguousBufferDecoderOptions, stream);

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
        return bitmap is not null ?
            Load(bitmap, $"file:{filename}", textureOptions ?? LoadTextureOptions(filename, resourceContainer)) :
            null;
    }
    public static Texture2d Create(Rgba32 color,
        string description,
        int width = 1,
        int height = 1,
        TextureOptions textureOptions = null)
    {
        if (width < 1 || height < 1) throw new InvalidOperationException($"Invalid texture size: {width}x{height}");

        textureOptions ??= TextureOptions.Default;
        if (textureOptions.PreMultiply)
        {
            var ratio = color.A / 255f;
            color = new((byte)(color.R * ratio), (byte)(color.G * ratio), (byte)(color.B * ratio), color.A);
        }

        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;

        GL.CreateTextures(TextureTarget.Texture2D, 1, out int textureId);
        GL.TextureStorage2D(textureId, 1, sRgb ? SizedInternalFormat.Srgb8 : SizedInternalFormat.Rgba8, width, height);

        if (useGlClearTex.Value) GL.ClearTexImage(textureId, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ref color);
        else
        {
            using UnmanagedBuffer<Rgba32> spanOwner = new(width * height);
            spanOwner.GetSpan().Fill(color);

            GL.TextureSubImage2D(textureId, 0, 0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, spanOwner.Address);
        }

        if (textureOptions.GenerateMipmaps) GL.GenerateTextureMipmap(textureId);
        textureOptions.ApplyParameters(textureId);

        return new(textureId, width, height, description);
    }
    public static Texture2d Load(Image<Rgba32> bitmap, string description, TextureOptions textureOptions = null)
    {
        var width = Math.Min(DrawState.MaxTextureSize, bitmap.Width);
        var height = Math.Min(DrawState.MaxTextureSize, bitmap.Height);

        textureOptions ??= TextureOptions.Default;
        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;
        var compress = DrawState.UseTextureCompression;

        GL.CreateTextures(TextureTarget.Texture2D, 1, out int textureId);
        GL.TextureStorage2D(textureId, 1, Unsafe.BitCast<PixelInternalFormat, SizedInternalFormat>(sRgb ?
            compress ? PixelInternalFormat.CompressedSrgbS3tcDxt1Ext : PixelInternalFormat.Srgb :
            compress ? PixelInternalFormat.CompressedRgbaS3tcDxt5Ext : PixelInternalFormat.Rgba), width, height);

        GL.TextureSubImage2D(textureId, 0, 0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte,
            ref MemoryMarshal.GetReference(bitmap.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(0)));

        if (textureOptions.GenerateMipmaps) GL.GenerateTextureMipmap(textureId);
        textureOptions.ApplyParameters(textureId);

        return new(textureId, width, height, description);
    }

    static DecoderOptions loadDecoderOptions()
    {
        DecoderOptions decoderOptions = new() { Configuration = Configuration.Default.Clone(), SkipMetadata = true };
        decoderOptions.Configuration.PreferContiguousImageBuffers = true;

        return decoderOptions;
    }

    #region IDisposable Support

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            DrawState.UnbindTexture(textureId);
            GL.DeleteTexture(textureId);

            if (disposing) disposed = true;
        }

        base.Dispose(disposing);
    }

    #endregion
}