using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using BrewLib.Data;
using osuTK.Graphics.OpenGL;

namespace BrewLib.Graphics.Textures;

public class Texture2d(int textureId, int width, int height, string description) : Texture2dRegion(null, new(0, 0, width, height), description), BindableTexture
{
    public int TextureId => disposed ? throw new ObjectDisposedException(GetType().FullName) : textureId;
    public TexturingModes TexturingMode => TexturingModes.Texturing2d;

    public override void Update(Bitmap bitmap, int x, int y, TextureOptions textureOptions)
    {
        if (x < 0 || y < 0 || x + bitmap.Width > Width || y + bitmap.Height > Height)
            throw new ArgumentException($"Invalid update bounds: {bitmap.Size} at {x},{y} overflows {Width}x{Height}");

        DrawState.BindPrimaryTexture(textureId, TexturingModes.Texturing2d);
        var data = bitmap.LockBits(new(default, bitmap.Size), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, data.Width, data.Height, osuTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, data.Scan0);
            GL.Finish();
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        DrawState.CheckError("updating texture");
    }

    public override string ToString() => $"Texture2d#{textureId} {Description} ({Width}x{Height})";

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

    public static Bitmap LoadBitmap(string filename, ResourceContainer resourceContainer = null)
    {
        if (File.Exists(filename)) using (FileStream stream = new(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) return new(stream);
        using (var stream = resourceContainer?.GetStream(filename, ResourceSource.Embedded))
        {
            if (stream is not null) return new(stream);

            Trace.TraceError($"Texture not found: {filename}");
            return null;
        }
    }

    public static TextureOptions LoadTextureOptions(string forBitmapFilename, ResourceContainer resourceContainer = null)
        => TextureOptions.Load(TextureOptions.GetOptionsFilename(forBitmapFilename), resourceContainer);

    public static Texture2d Load(string filename, ResourceContainer resourceContainer = null, TextureOptions textureOptions = null)
    {
        using var bitmap = LoadBitmap(filename, resourceContainer); 
        return bitmap is not null ? Load(bitmap, $"file:{filename}", textureOptions ?? LoadTextureOptions(filename, resourceContainer)) : null;
    }
    public static Texture2d Create(Color color, string description, int width = 1, int height = 1, TextureOptions textureOptions = null)
    {
        if (width < 1 || height < 1) throw new InvalidOperationException($"Invalid texture size: {width}x{height}");

        textureOptions ??= TextureOptions.Default;

        var channel = color.ToArgb();
        if (textureOptions.PreMultiply)
        {
            var ratio = color.A / 255f;
            channel = Color.FromArgb(color.A, (byte)(color.R * ratio), (byte)(color.G * ratio), (byte)(color.B * ratio)).ToArgb();
        }
        
        var textureId = GL.GenTexture();
        try
        {
            DrawState.BindTexture(textureId);
            unsafe
            {
                var count = width * height;
                var arr = stackalloc int[count];
                new Span<int>(arr, count).Fill(channel);

                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, osuTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, (nint)arr);
            }

            if (textureOptions.GenerateMipmaps)
            {
                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
                GL.Finish();
            }
            DrawState.CheckError("specifying texture");

            textureOptions.ApplyParameters(TextureTarget.Texture2D);
        }
        catch (Exception)
        {
            GL.DeleteTexture(textureId);
            DrawState.UnbindTexture(textureId);
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

        var data = bitmap.LockBits(new(0, 0, width, height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            GL.TexImage2D(TextureTarget.Texture2D, 0, sRgb ? PixelInternalFormat.SrgbAlpha : PixelInternalFormat.Rgba, data.Width, data.Height, 0, osuTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
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
            bitmap.UnlockBits(data);
        }
        return new(textureId, width, height, description);
    }
}