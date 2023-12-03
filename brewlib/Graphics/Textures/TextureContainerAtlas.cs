using BrewLib.Data;
using BrewLib.Util;
using System.Collections.Generic;
using System.Linq;

namespace BrewLib.Graphics.Textures;

public sealed class TextureContainerAtlas(ResourceContainer resourceContainer = null, TextureOptions textureOptions = null, int width = 512, int height = 512, int padding = 0, string description = nameof(TextureContainerAtlas)) : TextureContainer
{
    Dictionary<string, Texture2dRegion> textures = [];
    readonly Dictionary<TextureOptions, TextureMultiAtlas2d> atlases = [];

    public IEnumerable<string> ResourceNames => textures.Where(e => e.Value is not null).Select(e => e.Key);
    public event ResourceLoadedDelegate<Texture2dRegion> ResourceLoaded;

    public Texture2dRegion Get(string filename)
    {
        if (filename is null) return null;

        filename = PathHelper.WithStandardSeparators(filename);
        if (!textures.TryGetValue(filename, out Texture2dRegion texture))
        {
            var options = textureOptions ?? Texture2d.LoadTextureOptions(filename, resourceContainer) ?? TextureOptions.Default;
            if (!atlases.TryGetValue(options, out var atlas))
                atlases.Add(options, atlas = new(width, height, $"{description} (Option set {atlases.Count})", options, padding));

            using (var bitmap = Texture2d.LoadBitmap(filename, resourceContainer)) if (bitmap is not null) texture = atlas.AddRegion(bitmap, filename);

            textures.Add(filename, texture);
            ResourceLoaded?.Invoke(filename, texture);
        }
        return texture;
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            atlases.Dispose();
            textures.Clear();

            textures = null;
            disposed = true;
        }
    }

    #endregion
}