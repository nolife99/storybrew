namespace BrewLib.Graphics.Textures;

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IO;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
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

    public static async Task<Image<Rgba32>> LoadBitmapAsync(string filename,
        ResourceContainer resourceContainer = null,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.Exists(filename) ?
            new FileStream(filename,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan) :
            resourceContainer?.GetStream(filename, ResourceSource.Embedded);

        var config = Configuration.Default.Clone();
        config.PreferContiguousImageBuffers = true;
        
        var decoderOptions = new DecoderOptions()
        {
            Configuration = config
        };
        if (stream is not null)
            return await Image.LoadAsync<Rgba32>(decoderOptions, stream, cancellationToken).ConfigureAwait(false);

        SDL.LogWarn(LogCategory.Video, $"Texture not found: {filename}");
        return null;
    }

    public static TextureOptions LoadTextureOptions(string forBitmapFilename,
        ResourceContainer resourceContainer = null)
        => TextureOptions.Load(TextureOptions.GetOptionsFilename(forBitmapFilename), resourceContainer);
}