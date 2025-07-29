namespace BrewLib.Graphics.Textures;

using System;
using BrewLib.IO;
using BrewLib.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;

public sealed class TextureContainerAtlas : TextureContainer
{
    readonly string atlasDescription;
    readonly PooledDictionary<TextureOptions, TextureMultiAtlas2d> atlases;
    readonly int height, padding, width;

    readonly ResourceContainer resourceContainer;
    readonly TextureOptions textureOptions;
    readonly PooledDictionary<string, Texture2dRegion> textures;
    readonly PooledDictionary<string, Texture2dRegion>.AlternateLookup<ReadOnlySpan<char>> texturesLookup;

    public TextureContainerAtlas(ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null,
        int width = 1024,
        int height = 1024,
        int padding = 0,
        string atlasDescription = nameof(TextureContainerAtlas))
    {
        this.resourceContainer = resourceContainer;
        this.textureOptions = textureOptions;
        this.width = width;
        this.height = height;
        this.padding = padding;
        this.atlasDescription = atlasDescription;

        atlases = new();
        textures = new();
        texturesLookup = textures.GetAlternateLookup<ReadOnlySpan<char>>();
    }

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

    public Texture2dRegion Get(scoped ReadOnlySpan<char> filename)
    {
        PathHelper.WithStandardSeparatorsUnsafe(filename);

        if (texturesLookup.TryGetValue(filename, out var texture)) return texture;

        var str = filename.ToString();
        return textures[str] = Add(Texture2d.LoadBitmap(str, resourceContainer),
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