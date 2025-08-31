namespace BrewLib.Graphics.Textures;

using System;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;

public sealed class TextureMultiAtlas2d : IDisposable
{
    static bool firstOversize = true;

    readonly PooledList<TextureAtlas2d> atlases = [];
    readonly string description;
    readonly TextureOptions textureOptions;
    readonly int width, height, padding;
    PooledList<Texture2d> oversizeTextures;

    public TextureMultiAtlas2d(int width,
        int height,
        string description,
        TextureOptions textureOptions = null,
        int padding = 0)
    {
        this.width = width;
        this.height = height;
        this.description = description;
        this.textureOptions = textureOptions;
        this.padding = padding;

        pushAtlas();
    }

    public Texture2dRegion AddRegion(Image<Rgba32> bitmap)
    {
        if (bitmap.Width * bitmap.Height > width * height) return loadOversized(bitmap);

        var fragmentation = 0f;
        foreach (var atlas in atlases)
        {
            var region = atlas.AddRegion(bitmap);
            if (region is not null) return region;

            fragmentation = float.Max(fragmentation, atlas.Fragmentation);
        }

        SDL.LogInfo(SDL.LogCategory.Video, $"{description} full, adding an atlas (max {fragmentation:P2} fragmented)");
        return pushAtlas().AddRegion(bitmap);
    }

    Texture2d loadOversized(Image<Rgba32> bitmap)
    {
        SDL.LogWarn(SDL.LogCategory.Video, $"Bitmap \"{bitmap.Size}\" doesn't fit in this atlas");

        var texture = Texture2d.Load(bitmap, textureOptions);
        (oversizeTextures ??= []).Add(texture);
        return texture;
    }

    TextureAtlas2d pushAtlas()
    {
        TextureAtlas2d atlas = new(width, height, textureOptions, padding);
        atlases.Add(atlas);
        return atlas;
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        using (atlases)
            foreach (var atlas in atlases)
                atlas.Dispose();

        if (oversizeTextures is not null)
            using (oversizeTextures)
                foreach (var texture in oversizeTextures)
                    texture.Dispose();

        disposed = true;
    }

    #endregion
}