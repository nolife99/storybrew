namespace BrewLib.Graphics.Textures;

using System;
using IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;

public sealed class TextureContainerSeparate(ResourceContainer resourceContainer = null,
    TextureOptions textureOptions = null) : TextureContainer
{
    readonly PooledDictionary<int, Texture2d> textures = new();

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

    public Texture2dRegion Get(scoped ReadOnlySpan<char> filename)
    {
        var hashCode = string.GetHashCode(filename);
        if (textures.TryGetValue(hashCode, out var texture)) return texture;

        return textures[hashCode] = Texture2d.Load(filename.ToString(), resourceContainer, textureOptions);
    }

    public Texture2dRegion Add(Image<Rgba32> bitmap, TextureOptions options = null)
        => Texture2d.Load(bitmap, textureOptions);

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