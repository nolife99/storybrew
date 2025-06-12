namespace BrewLib.Graphics.Textures;

using System;
using IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;
using Util;

public sealed class TextureContainerAtlas(ResourceContainer resourceContainer = null,
    TextureOptions textureOptions = null,
    int width = 1024,
    int height = 1024,
    int padding = 0,
    string atlasDescription = nameof(TextureContainerAtlas)) : TextureContainer
{
    readonly PooledDictionary<TextureOptions, TextureMultiAtlas2d> atlases = new();
    readonly PooledDictionary<int, Texture2dRegion> textures = new();

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

    public Texture2dRegion Get(ReadOnlySpan<char> filename)
    {
        PathHelper.WithStandardSeparatorsUnsafe(filename);

        var hashCode = string.GetHashCode(filename);
        if (textures.TryGetValue(hashCode, out var texture)) return texture;

        var str = filename.ToString();
        return textures[hashCode] = Add(Texture2d.LoadBitmap(str, resourceContainer),
            textureOptions ?? Texture2d.LoadTextureOptions(str, resourceContainer));
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