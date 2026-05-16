namespace BrewLib.Graphics.Textures;

using System.IO;
using BrewLib.IO;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

public static class TextureLoader
{
    public static Image<Rgba32> LoadBitmap(string filename, ResourceContainer resourceContainer = null)
    {
        using var stream = File.Exists(filename) ?
            new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 0, FileOptions.SequentialScan) :
            resourceContainer?.GetStream(filename, ResourceSource.Embedded);

        if (stream is not null) return Image.Load<Rgba32>(stream);

        SDL.LogWarn(LogCategory.Video, $"Texture not found: {filename}");
        return null;
    }

    public static TextureOptions LoadTextureOptions(string forBitmapFilename,
        ResourceContainer resourceContainer = null)
        => TextureOptions.Load(TextureOptions.GetOptionsFilename(forBitmapFilename), resourceContainer);
}
