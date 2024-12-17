namespace BrewLib.Graphics.Textures;

using System.Collections.Generic;
using System.Runtime.InteropServices;
using IO;
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
                {
                    var size = texture.Size;
                    pixels += size.X * size.Y;
                }

            return pixels / 1024 / 1024 * 4;
        }
    }

    public Texture2dRegion Get(string filename)
    {
        ref var texture = ref CollectionsMarshal.GetValueRefOrAddDefault(textures, filename, out var exists);
        if (exists) return texture;
        return texture = Texture2d.Load(filename, resourceContainer, textureOptions);
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