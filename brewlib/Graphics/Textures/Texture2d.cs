namespace BrewLib.Graphics.Textures;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Data;
using osuTK.Graphics.OpenGL;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

public class Texture2d(int textureId, int width, int height, string description)
    : Texture2dRegion(null, new(0, 0, width, height), description), BindableTexture
{
    public int TextureId => disposed ? throw new ObjectDisposedException(description) : textureId;

    public override void Update(Bitmap bitmap, int x, int y, TextureOptions textureOptions)
    {
        DrawState.BindPrimaryTexture(textureId);

        textureOptions ??= TextureOptions.Default;
        textureOptions.WithBitmap(bitmap, b =>
        {
            var data = b.LockBits(new(default, b.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, data.Width, data.Height,
                osuTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

            GL.Finish();
            b.UnlockBits(data);
        });

        DrawState.CheckError("updating texture");
    }

    public static Bitmap LoadBitmap(string filename, ResourceContainer resourceContainer = null)
    {
        if (File.Exists(filename))
            using (var stream = File.OpenRead(filename))
                return new(stream);
        using (var stream = resourceContainer?.GetStream(filename, ResourceSource.Embedded))
        {
            if (stream is not null) return new(stream);

            Trace.TraceWarning($"Texture not found: {filename}");
            return null;
        }
    }

    public static TextureOptions LoadTextureOptions(string forBitmapFilename,
        ResourceContainer resourceContainer = null)
        => TextureOptions.Load(TextureOptions.GetOptionsFilename(forBitmapFilename), resourceContainer);

    public static Texture2d Load(string filename, ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null)
    {
        using var bitmap = LoadBitmap(filename, resourceContainer);
        return bitmap is not null ? Load(bitmap, $"file:{filename}",
            textureOptions ?? LoadTextureOptions(filename, resourceContainer)) : null;
    }

    public static Texture2d Create(Color color, string description, int width = 1, int height = 1,
        TextureOptions textureOptions = null)
    {
        if (width < 1 || height < 1) throw new InvalidOperationException($"Invalid texture size: {width}x{height}");

        textureOptions ??= TextureOptions.Default;
        if (textureOptions.PreMultiply)
        {
            var ratio = color.A / 255f;
            color = Color.FromArgb(color.A, (byte)(color.R * ratio), (byte)(color.G * ratio), (byte)(color.B * ratio));
        }

        var textureId = GL.GenTexture();
        var area = width * height;
        var arr = ArrayPool<int>.Shared.Rent(area);

        try
        {
            DrawState.BindTexture(textureId);

            Array.Fill(arr, color.ToArgb(), 0, area);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                osuTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, arr);

            if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.Finish();

            DrawState.CheckError("specifying texture");
            textureOptions.ApplyParameters(TextureTarget.Texture2D);
        }
        catch
        {
            GL.DeleteTexture(textureId);
            DrawState.UnbindTexture(textureId);
            throw;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(arr);
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

        try
        {
            textureOptions.WithBitmap(bitmap, b =>
            {
                var data = b.LockBits(new(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                GL.TexImage2D(TextureTarget.Texture2D, 0,
                    sRgb ? PixelInternalFormat.SrgbAlpha : PixelInternalFormat.Rgba, data.Width, data.Height, 0,
                    osuTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
                if (textureOptions.GenerateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

                GL.Finish();
                b.UnlockBits(data);
            });

            DrawState.CheckError("specifying texture");
            textureOptions.ApplyParameters(TextureTarget.Texture2D);
        }
        catch
        {
            GL.DeleteTexture(textureId);
            DrawState.UnbindTexture(textureId);
            throw;
        }

        return new(textureId, width, height, description);
    }

#region IDisposable Support

    bool disposed;

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing) DrawState.UnbindTexture(this);
            GL.DeleteTexture(textureId);
            disposed = true;
        }

        base.Dispose(disposing);
    }

#endregion
}