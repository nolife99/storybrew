namespace BrewLib.Graphics.Textures;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
                {
                    var size = texture.Size;
                    pixels += size.X * size.Y;
                }

            return pixels / 1024 / 1024 * 4;
        }
    }

    public Texture2dRegion Get(string filename)
    {
        ref var texture = ref CollectionsMarshal.GetValueRefOrNullRef(textures, filename);
        if (Unsafe.IsNullRef(ref texture))
            return textures[filename] = Texture2d.Load(filename, resourceContainer, textureOptions);

        return texture;
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