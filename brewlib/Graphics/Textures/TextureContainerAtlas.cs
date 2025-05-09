namespace BrewLib.Graphics.Textures;

using Collections.Pooled;
using IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Util;

public sealed class TextureContainerAtlas(ResourceContainer resourceContainer = null,
    TextureOptions textureOptions = null,
    int width = 1024,
    int height = 1024,
    int padding = 0,
    string atlasDescription = nameof(TextureContainerAtlas)) : TextureContainer
{
    readonly PooledDictionary<TextureOptions, TextureMultiAtlas2d> atlases = new();
    readonly PooledDictionary<string, Texture2dRegion> textures = new();

    public float UncompressedMemoryUseMb
    {
        get
        {
            var sum = 0f;
            foreach (var texture in textures.Values)
                if (texture is not null)
                {
                    var size = texture.Size;
                    sum += size.X * size.Y;
                }

            return sum / 1024 / 1024;
        }
    }

    public Texture2dRegion Get(string filename)
    {
        // TODO: Fix the shit performance of this??

        PathHelper.WithStandardSeparatorsUnsafe(filename);
        if (textures.TryGetValue(filename, out var texture)) return texture;

        return textures[filename] = Add(Texture2d.LoadBitmap(filename, resourceContainer),
            textureOptions ?? Texture2d.LoadTextureOptions(filename, resourceContainer));
    }

    public Texture2dRegion Add(Image<Rgba32> bitmap, TextureOptions options)
    {
        if (bitmap is null) return null;

        options ??= TextureOptions.Default;
        if (!atlases.TryGetValue(options, out var atlas))
            atlases[options] = atlas = new(width,
                height,
                $"{atlasDescription} (Option set {atlases.Count})",
                options,
                padding);

        return atlas.AddRegion(bitmap);
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var atlas in atlases.Values) atlas.Dispose();
        atlases.Dispose();

        textures.Dispose();
        disposed = true;
    }

    #endregion
}