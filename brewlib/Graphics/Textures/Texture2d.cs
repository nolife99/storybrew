namespace BrewLib.Graphics.Textures;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using CommunityToolkit.HighPerformance.Buffers;
using Data;
using OpenTK.Graphics.OpenGL;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

public sealed class Texture2d(int textureId, int width, int height, string description)
    : Texture2dRegion(null, new(0, 0, width, height)), BindableTexture
{
    public int TextureId => disposed ? throw new ObjectDisposedException(description) : textureId;

    public void Update(Bitmap bitmap, int x, int y, TextureOptions textureOptions)
    {
        DrawState.BindTexture(textureId);

        var data = bitmap.LockBits(new(default, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, data.Width, data.Height, OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
            PixelType.UnsignedByte, data.Scan0);

        bitmap.UnlockBits(data);
    }
    public static Bitmap LoadBitmap(string filename, ResourceContainer resourceContainer = null)
    {
        if (File.Exists(filename))
            using (var stream = File.OpenRead(filename))
                return new(stream);

        using (var stream = resourceContainer?.GetStream(filename, ResourceSource.Embedded))
            if (stream is not null)
                return new(stream);

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
    public static unsafe Texture2d Create(Color color,
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
            color = Color.FromArgb(color.A, (int)(color.R * ratio), (int)(color.G * ratio), (int)(color.B * ratio));
        }

        var textureId = GL.GenTexture();
        using (var spanOwner = SpanOwner<int>.Allocate(width * height))
            try
            {
                var arr = spanOwner.Span;
                DrawState.BindTexture(textureId);

                arr.Fill(color.ToArgb());
                fixed (void* ptr = arr)
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                        OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, (nint)ptr);

                if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                textureOptions.ApplyParameters(TextureTarget.Texture2D);
            }
            catch
            {
                DrawState.UnbindTexture(textureId);
                GL.DeleteTexture(textureId);
                throw;
            }

        return new(textureId, width, height, description);
    }
    public static Texture2d Load(Bitmap bitmap, string description, TextureOptions textureOptions = null)
    {
        var width = Math.Min(DrawState.MaxTextureSize, bitmap.Width);
        var height = Math.Min(DrawState.MaxTextureSize, bitmap.Height);

        textureOptions ??= TextureOptions.Default;
        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;

        var textureId = GL.GenTexture();
        DrawState.BindTexture(textureId);

        var data = bitmap.LockBits(new(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        GL.TexImage2D(TextureTarget.Texture2D, 0, sRgb ? PixelInternalFormat.SrgbAlpha : PixelInternalFormat.Rgba, data.Width,
            data.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

        bitmap.UnlockBits(data);

        if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        textureOptions.ApplyParameters(TextureTarget.Texture2D);

        return new(textureId, width, height, description);
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