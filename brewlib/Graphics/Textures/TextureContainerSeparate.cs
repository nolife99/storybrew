namespace BrewLib.Graphics.Textures;

using System.Collections.Generic;
using Data;
using Util;

public sealed class TextureContainerSeparate(ResourceContainer resourceContainer = null, TextureOptions textureOptions = null)
    : TextureContainer
{
    readonly Dictionary<string, Texture2d> textures = [];

    public float UncompressedMemoryUseMb
    {
        get
        {
            var pixels = 0f;
            foreach (var texture in textures.Values)
                if (texture is not null)
                    pixels += texture.Size.X * texture.Size.Y;

            return pixels / 1024 / 1024 * 4;
        }
    }

    public Texture2dRegion Get(string filename)
    {
        if (textures.TryGetValue(filename, out var texture)) return texture;
        return textures[filename] = Texture2d.Load(filename, resourceContainer, textureOptions);
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;
        textures.Dispose();
        disposed = true;
    }

    #endregion
}