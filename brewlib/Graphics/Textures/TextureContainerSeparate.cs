namespace BrewLib.Graphics.Textures;

using Collections.Pooled;
using IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public sealed class TextureContainerSeparate(ResourceContainer resourceContainer = null,
    TextureOptions textureOptions = null) : TextureContainer
{
    readonly PooledDictionary<string, Texture2d> textures = [];

    public float UncompressedMemoryUseMb
    {
        get
        {
            var pixels = 0f;
            foreach (var texture in textures.Values)
                if (texture is not null)
                {
                    var size = texture.Size;
                    pixels += size.X * size.Y;
                }

            return pixels / 1024 / 1024 * 4;
        }
    }

    public Texture2dRegion Get(string filename)
    {
        if (textures.TryGetValue(filename, out var texture)) return texture;

        return textures[filename] = Texture2d.Load(filename, resourceContainer, textureOptions);
    }

    public Texture2dRegion Add(Image<Rgba32> bitmap, string description, TextureOptions options)
        => Texture2d.Load(bitmap, description, textureOptions);

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var texture in textures.Values) texture.Dispose();
        textures.Dispose();
        disposed = true;
    }

    #endregion
}