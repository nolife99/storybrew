using System.Collections.Generic;
using System.Linq;
using BrewLib.Data;
using BrewLib.Util;

namespace BrewLib.Graphics.Textures;

public sealed class TextureContainerSeparate(ResourceContainer resourceContainer = null, TextureOptions textureOptions = null) : TextureContainer
{
    Dictionary<string, Texture2d> textures = [];

    public IEnumerable<string> ResourceNames => textures.Where(e => e.Value is not null).Select(e => e.Key);
    public event ResourceLoadedDelegate<Texture2dRegion> ResourceLoaded;

    public Texture2dRegion Get(string filename)
    {
        filename = PathHelper.WithStandardSeparators(filename);
        if (!textures.TryGetValue(filename, out var texture))
        {
            textures.Add(filename, texture = Texture2d.Load(filename, resourceContainer, textureOptions));
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
            textures.Dispose();

            textures = null;
            disposed = true;
        }
    }

    #endregion
}