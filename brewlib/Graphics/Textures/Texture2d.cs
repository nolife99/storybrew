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
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

public sealed class Texture2d(int textureId, int width, int height, string description) : Texture2dRegion(null,
    new(0, 0, width, height))
{
    static readonly bool useGlClearTex = GLFW.ExtensionSupported("GL_ARB_clear_texture");
    static readonly DecoderOptions decoderOptions = new() { Configuration = Configuration.Default.Clone() };

    bool isResident;

    public int TextureId => disposed ? throw new ObjectDisposedException(description) : textureId;

    public void Update(Image<Rgba32> bitmap, int x, int y)
    {
        var buffer = bitmap.Frames.RootFrame.PixelBuffer;

        GL.BindTexture(TextureTarget.Texture2D, textureId);
        GL.TexSubImage2D(TextureTarget.Texture2D,
            0,
            x,
            y,
            buffer.Width,
            buffer.Height,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            ref MemoryMarshal.GetReference(buffer.DangerousGetRowSpan(0)));
    }

    static Image<Rgba32> LoadBitmap(string filename, ResourceContainer resourceContainer = null)
    {
        using var stream = File.Exists(filename) ?
            File.OpenRead(filename) :
            resourceContainer?.GetStream(filename, ResourceSource.Embedded);

        if (stream is not null)
        {
            decoderOptions.Configuration.PreferContiguousImageBuffers = true;
            return Image.Load<Rgba32>(decoderOptions, stream);
        }

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

        return new(textureId, width, height, description);
    }

    public static Texture2d Load(Image<Rgba32> bitmap, string description, TextureOptions textureOptions = null)
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
        GL.TexSubImage2D(TextureTarget.Texture2D,
            0,
            0,
            0,
            width,
            height,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            ref MemoryMarshal.GetReference(buffer.DangerousGetRowSpan(0)));

        if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        textureOptions.ApplyParameters(TextureTarget.Texture2D);

        return new(textureId, width, height, description);
    }

    public void MakeBindlessResident()
    {
        if (GL.Arb.IsTextureHandleResident(BindlessTextureHandle)) return;

        GL.Arb.MakeTextureHandleResident(BindlessTextureHandle);
    }

    #region IDisposable Support

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (GL.Arb.IsTextureHandleResident(BindlessTextureHandle))
                GL.Arb.MakeTextureHandleNonResident(BindlessTextureHandle);

            GL.DeleteTexture(textureId);

            if (disposing) disposed = true;
        }

        base.Dispose(disposing);
    }

    #endregion
}