namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Generic;
using Data;
using Util;

public sealed class TextureContainerSeparate(ResourceContainer resourceContainer = null, TextureOptions textureOptions = null)
    : TextureContainer
{
    Dictionary<string, Texture2d> textures = [];

    public float UncompressedMemoryUseMb
    {
        get
        {
            var pixels = 0f;
            foreach (var texture in textures.Values)
                if (texture is not null)
                    pixels += texture.Size.X * texture.Size.Y;

            return pixels / 262144;
        }
    }

    public event ResourceLoadedDelegate<Texture2dRegion> ResourceLoaded;

    public Texture2dRegion Get(string filename)
    {
        if (textures.TryGetValue(filename, out var texture)) return texture;
        textures[filename] = texture = Texture2d.Load(filename, resourceContainer, textureOptions);
        ResourceLoaded?.Invoke(filename, texture);

        return texture;
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;
        textures.Dispose();
        textures = null;

        GC.SuppressFinalize(this);
        disposed = true;
    }

    #endregion
}