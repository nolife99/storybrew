namespace BrewLib.Graphics.Textures;

using System;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Backend.OpenGL;
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

    readonly ITextureFactory textureFactory;
    readonly ResourceContainer resourceContainer;
    readonly TextureOptions textureOptions;
    readonly PooledDictionary<string, ITextureRegion> textures;
    readonly PooledDictionary<string, ITextureRegion>.AlternateLookup<ReadOnlySpan<char>> texturesLookup;

    public TextureContainerAtlas(ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null,
        int width = 1024,
        int height = 1024,
        int padding = 0,
        string atlasDescription = nameof(TextureContainerAtlas))
        : this(DrawState.Backend?.TextureFactory ?? new OpenGlTextureFactory(DrawState.Backend),
            resourceContainer,
            textureOptions,
            width,
            height,
            padding,
            atlasDescription)
    {
    }

    public TextureContainerAtlas(ITextureFactory textureFactory,
        ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null,
        int width = 1024,
        int height = 1024,
        int padding = 0,
        string atlasDescription = nameof(TextureContainerAtlas))
    {
        this.textureFactory = textureFactory ?? DrawState.Backend?.TextureFactory ??
            new OpenGlTextureFactory(DrawState.Backend);
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

    public long UncompressedMemoryUse
    {
        get
        {
            var sum = 0L;
            foreach (var texture in textures.Values)
                if (texture is not null)
                {
                    var size = texture.Size;
                    sum += size.Width * size.Height;
                }

            return sum * 4;
        }
    }

    public ITextureRegion Get(scoped ReadOnlySpan<char> filename)
    {
        PathHelper.WithStandardSeparatorsUnsafe(filename);

        if (texturesLookup.TryGetValue(filename, out var texture)) return texture;

        var str = filename.ToString();

        using var bitmap = TextureLoader.LoadBitmap(str, resourceContainer);
        return textures[str] = Add(bitmap, textureOptions ?? TextureLoader.LoadTextureOptions(str, resourceContainer));
    }

    public ITextureRegion Add(Image<Rgba32> bitmap, TextureOptions options)
    {
        if (bitmap is null) return null;

        options ??= TextureOptions.Default;
        if (!atlases.TryGetValue(options, out var atlas))
            atlases[options] = atlas = new(textureFactory,
                width,
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
