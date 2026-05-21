namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Concurrent;
using Backend.OpenGL;
using IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;
using Util;

public sealed class TextureContainerAtlas : TextureContainer
{
    readonly string atlasDescription;
    readonly PooledDictionary<TextureOptions, TextureMultiAtlas2d> atlases;
    readonly int height, padding, width;

    readonly ITextureFactory textureFactory;
    readonly ResourceContainer resourceContainer;
    readonly TextureOptions textureOptions;
    readonly ConcurrentDictionary<string, ITextureRegion> textures;
    readonly ConcurrentDictionary<string, ITextureRegion>.AlternateLookup<ReadOnlySpan<char>> texturesLookup;

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
        textures = new(StringComparer.Ordinal);
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
        var options = textureOptions ?? TextureLoader.LoadTextureOptions(str, resourceContainer);
        lock (atlases)
        {
            if (texturesLookup.TryGetValue(str, out texture)) return texture;

            texture = Add(bitmap, options);
            textures[str] = texture;
            return texture;
        }
    }

    public bool TryGetLoaded(scoped ReadOnlySpan<char> filename, out ITextureRegion texture)
    {
        PathHelper.WithStandardSeparatorsUnsafe(filename);
        return texturesLookup.TryGetValue(filename, out texture);
    }

    public ITextureRegion Add(Image<Rgba32> bitmap, TextureOptions options)
    {
        if (bitmap is null) return null;

        lock (atlases)
        {
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
    }

    public ITextureRegion Add(string filename, Image<Rgba32> bitmap, TextureOptions options = null)
    {
        if (bitmap is null) return null;
        filename = PathHelper.WithStandardSeparators(filename);

        if (texturesLookup.TryGetValue(filename, out var texture)) return texture;

        lock (atlases)
        {
            if (texturesLookup.TryGetValue(filename, out texture)) return texture;

            texture = Add(bitmap, options ?? textureOptions);
            textures[filename] = texture;
            return texture;
        }
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var atlas in atlases.Values) atlas.Dispose();
        atlases.Dispose();

        disposed = true;
    }

    #endregion
}
