namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public sealed class TextureMultiAtlas2d : IDisposable
{
    static bool firstOversize = true;

    readonly List<TextureAtlas2d> atlases = [];
    readonly string description;
    readonly TextureOptions textureOptions;
    readonly int width, height, padding;
    List<Texture2d> oversizeTextures;

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

    public Texture2dRegion AddRegion(Image<Rgba32> bitmap, string description)
    {
        if (bitmap.Width * bitmap.Height > width * height / 2)
        {
            if (firstOversize)
            {
                if (bitmap.Width * bitmap.Height > width * height) return loadOversized(bitmap, description);
                firstOversize = false;
            }
            else return loadOversized(bitmap, description);
        }

        foreach (var atlas in atlases)
        {
            var region = atlas.AddRegion(bitmap);
            if (region is not null) return region;
        }

        Trace.WriteLine($"{this.description} full, adding an atlas");
        return pushAtlas().AddRegion(bitmap);
    }

    Texture2d loadOversized(Image<Rgba32> bitmap, string description)
    {
        Trace.TraceWarning($"Bitmap \"{description}\" doesn't fit in this atlas");

        var texture = Texture2d.Load(bitmap, description, textureOptions);
        (oversizeTextures ??= []).Add(texture);
        return texture;
    }

    TextureAtlas2d pushAtlas()
    {
        TextureAtlas2d atlas = new(width, height, $"{description}#{atlases.Count + 1}", textureOptions, padding);
        atlases.Add(atlas);
        return atlas;
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var atlas in atlases) atlas.Dispose();
        if (oversizeTextures is not null)
            foreach (var texture in oversizeTextures)
                texture.Dispose();

        disposed = true;
    }

    #endregion
}