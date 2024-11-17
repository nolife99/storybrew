namespace BrewLib.Graphics.Textures;

using System;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.HighPerformance.Buffers;
using Data;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

public sealed class Texture2d(int textureId, int width, int height, string description)
    : Texture2dRegion(null, new(0, 0, width, height)), BindableTexture
{
    public int TextureId => disposed ? throw new ObjectDisposedException(description) : textureId;

    public unsafe void Update(Image<Rgba32> bitmap, int x, int y, TextureOptions textureOptions)
    {
        DrawState.BindTexture(textureId);

        bitmap.DangerousTryGetSinglePixelMemory(out var memory);
        using var pinned = memory.Pin();

        GL.TexSubImage2D(TextureTarget.Texture2D, 0, x, y, bitmap.Width, bitmap.Height, PixelFormat.Rgba,
            PixelType.UnsignedByte, (nint)pinned.Pointer);
    }
    public static Image<Rgba32> LoadBitmap(string filename, ResourceContainer resourceContainer = null)
    {
        DecoderOptions options = new();
        options.Configuration.PreferContiguousImageBuffers = true;

        if (File.Exists(filename))
            using (var stream = File.OpenRead(filename))
                return Image.Load<Rgba32>(options, stream);

        using (var stream = resourceContainer?.GetStream(filename, ResourceSource.Embedded))
            if (stream is not null)
                return Image.Load<Rgba32>(options, stream);

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
    public static unsafe Texture2d Create(Rgba32 color,
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
            color = new((int)(color.R * ratio), (int)(color.G * ratio), (int)(color.B * ratio), color.A);
        }

        var textureId = GL.GenTexture();
        using (var spanOwner = SpanOwner<uint>.Allocate(width * height))
            try
            {
                var arr = spanOwner.Span;
                DrawState.BindTexture(textureId);

                arr.Fill(color.PackedValue);
                fixed (void* ptr = arr)
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, (nint)ptr);

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
    public static unsafe Texture2d Load(Image<Rgba32> bitmap, string description, TextureOptions textureOptions = null)
    {
        var width = Math.Min(DrawState.MaxTextureSize, bitmap.Width);
        var height = Math.Min(DrawState.MaxTextureSize, bitmap.Height);

        textureOptions ??= TextureOptions.Default;
        var sRgb = textureOptions.Srgb && DrawState.ColorCorrected;

        var textureId = GL.GenTexture();
        DrawState.BindTexture(textureId);

        if (!bitmap.DangerousTryGetSinglePixelMemory(out var memory))
            throw new InvalidDataException("Backing buffer is not contiguous.");

        using var pinned = memory.Pin();
        GL.TexImage2D(TextureTarget.Texture2D, 0, sRgb ? PixelInternalFormat.SrgbAlpha : PixelInternalFormat.Rgba, width,
            height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, (nint)pinned.Pointer);

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