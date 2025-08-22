namespace BrewLib.Graphics.Textures;

using System;
using BrewLib.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;

public sealed class TextureContainerSeparate : TextureContainer
{
    readonly ResourceContainer resourceContainer;
    readonly TextureOptions textureOptions;
    readonly PooledDictionary<string, Texture2d> textures;
    readonly PooledDictionary<string, Texture2d>.AlternateLookup<ReadOnlySpan<char>> texturesLookup;

    public TextureContainerSeparate(ResourceContainer resourceContainer = null, TextureOptions textureOptions = null)
    {
        this.resourceContainer = resourceContainer;
        this.textureOptions = textureOptions;

        textures = new();
        texturesLookup = textures.GetAlternateLookup<ReadOnlySpan<char>>();
    }

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
        if (texturesLookup.TryGetValue(filename, out var texture)) return texture;

        var str = filename.ToString();
        return textures[str] = Texture2d.Load(str, resourceContainer, textureOptions);
    }

    public Texture2dRegion Add(Image<Rgba32> bitmap, TextureOptions options = null)
        => Texture2d.Load(bitmap, textureOptions);

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var texture in textures.Values) texture?.Dispose();
        textures.Dispose();
        disposed = true;
    }

    #endregion
}