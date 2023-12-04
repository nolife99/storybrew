using BrewLib.Data;
using osuTK.Graphics.OpenGL;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Bitmap = System.Drawing.Bitmap;

namespace BrewLib.Graphics.Textures;

public class Texture2d(int textureId, int width, int height, string description) : Texture2dRegion(null, new(0, 0, width, height), description), BindableTexture
{
    public int TextureId => disposed ? throw new ObjectDisposedException(GetType().FullName) : textureId;
    public TexturingModes TexturingMode => TexturingModes.Texturing2d;

    public override void Update(Bitmap bitmap, int x, int y, TextureOptions textureOptions)
    {
        if (bitmap.Width < 1 || bitmap.Height < 1) throw new InvalidOperationException($"Invalid bitmap size: {bitmap.Size}");
        if (x < 0 || y < 0 || x + bitmap.Width > Width || y + bitmap.Height > Height)
            throw new InvalidOperationException($"Invalid update bounds: {bitmap.Size} at {x},{y} overflows {Width}x{Height}");

        DrawState.BindPrimaryTexture(textureId, TexturingModes.Texturing2d);

        textureOptions ??= TextureOptions.Default;
        textureOptions.WithBitmap(bitmap, b =>
        {
            var data = b.LockBits(new(default, b.Size), ImageLockMode.ReadOnly, b.PixelFormat);
            GL.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, b.Width, b.Height, osuTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, data.Scan0);
            GL.Finish();
            b.UnlockBits(data);
        });

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
        if (File.Exists(filename)) using (var stream = File.OpenRead(filename)) return new Bitmap(stream, false);

        if (resourceContainer is null) return null;
        using (var stream = resourceContainer.GetStream(filename, ResourceSource.Embedded))
        {
            if (stream is null)
            {
                Trace.TraceError($"Texture not found: {filename}");
                return null;
            }
            return new(stream, false);
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
            channel = (color.A << 24) | ((int)(color.R * ratio) << 16) | ((int)(color.G * ratio) << 8) | (int)(color.B * ratio);
        }
        
        var textureId = GL.GenTexture();
        try
        {
            DrawState.BindTexture(textureId);

            var array = GC.AllocateUninitializedArray<int>(width * height);
            new Span<int>(array).Fill(channel);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, osuTK.Graphics.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, array);
            
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
        ArgumentNullException.ThrowIfNull(bitmap);

        textureOptions ??= TextureOptions.Default;
        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;

        var width = Math.Min(DrawState.MaxTextureSize, bitmap.Width);
        var height = Math.Min(DrawState.MaxTextureSize, bitmap.Height);

        var textureId = GL.GenTexture();
        try
        {
            DrawState.BindTexture(textureId);

            textureOptions.WithBitmap(bitmap, b =>
            {
                var data = b.LockBits(new(0, 0, width, height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                GL.TexImage2D(TextureTarget.Texture2D, 0, sRgb ? PixelInternalFormat.SrgbAlpha : PixelInternalFormat.Rgba, b.Width, b.Height, 0, osuTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
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
}