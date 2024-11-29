namespace BrewLib.Graphics.Textures;

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Data;
using Util;

public sealed class TextureContainerAtlas(ResourceContainer resourceContainer = null,
    TextureOptions textureOptions = null,
    int width = 512,
    int height = 512,
    int padding = 0,
    string description = nameof(TextureContainerAtlas)) : TextureContainer
{
    readonly Dictionary<TextureOptions, TextureMultiAtlas2d> atlases = [];
    readonly Dictionary<string, Texture2dRegion> textures = [];

    public float UncompressedMemoryUseMb => textures.Values.Sum(texture => texture.Size.X * texture.Size.Y) / 1024 / 1024;

    public Texture2dRegion Get(string filename)
    {
        PathHelper.WithStandardSeparatorsUnsafe(filename);
        ref var texture = ref CollectionsMarshal.GetValueRefOrAddDefault(textures, filename, out var exists);
        if (exists) return texture;

        var options = textureOptions ?? Texture2d.LoadTextureOptions(filename, resourceContainer) ?? TextureOptions.Default;
        ref var atlas = ref CollectionsMarshal.GetValueRefOrAddDefault(atlases, options, out exists);
        if (!exists) atlas = new(width, height, $"{description} (Option set {atlases.Count})", options, padding);

        using var bitmap = Texture2d.LoadBitmap(filename, resourceContainer);
        if (bitmap is not null) texture = atlas.AddRegion(bitmap, filename);

        return texture;
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;
        atlases.Dispose();
        disposed = true;
    }

    #endregion
}