namespace BrewLib.Graphics.Textures;

using System;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Backend.OpenGL;
using BrewLib.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;

public sealed class TextureContainerSeparate : TextureContainer
{
    readonly ITextureFactory textureFactory;
    readonly ResourceContainer resourceContainer;
    readonly TextureOptions textureOptions;
    readonly PooledDictionary<string, ITextureRegion> textures;
    readonly PooledDictionary<string, ITextureRegion>.AlternateLookup<ReadOnlySpan<char>> texturesLookup;

    public TextureContainerSeparate(ResourceContainer resourceContainer = null, TextureOptions textureOptions = null)
        : this(DrawState.Backend?.TextureFactory ?? new OpenGlTextureFactory(DrawState.Backend),
            resourceContainer,
            textureOptions)
    {
    }

    public TextureContainerSeparate(ITextureFactory textureFactory,
        ResourceContainer resourceContainer = null,
        TextureOptions textureOptions = null)
    {
        this.textureFactory = textureFactory ?? DrawState.Backend?.TextureFactory ??
            new OpenGlTextureFactory(DrawState.Backend);
        this.resourceContainer = resourceContainer;
        this.textureOptions = textureOptions;

        textures = new();
        texturesLookup = textures.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public long UncompressedMemoryUse
    {
        get
        {
            var pixels = 0L;
            foreach (var texture in textures.Values)
                if (texture is not null)
                {
                    var size = texture.Size;
                    pixels += size.Width * size.Height;
                }

            return pixels * 4;
        }
    }

    public ITextureRegion Get(scoped ReadOnlySpan<char> filename)
    {
        if (texturesLookup.TryGetValue(filename, out var texture)) return texture;

        var str = filename.ToString();
        using var bitmap = TextureLoader.LoadBitmap(str, resourceContainer);
        return textures[str] = bitmap is not null ?
            textureFactory.Load(bitmap, textureOptions ?? TextureLoader.LoadTextureOptions(str, resourceContainer)) :
            null;
    }

    public ITextureRegion Add(Image<Rgba32> bitmap, TextureOptions options = null)
        => bitmap is not null ? textureFactory.Load(bitmap, options ?? textureOptions) : null;

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
