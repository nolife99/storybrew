namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

public sealed class TextureMultiAtlas2d : IDisposable
{
    readonly List<TextureAtlas2d> atlases = [];
    readonly string description;
    readonly TextureOptions textureOptions;
    readonly int width, height, padding;
    List<Texture2d> oversizeTextures;

    public TextureMultiAtlas2d(int width, int height, string description, TextureOptions textureOptions = null, int padding = 0)
    {
        this.width = width;
        this.height = height;
        this.description = description;
        this.textureOptions = textureOptions;
        this.padding = padding;

        pushAtlas();
    }

    public Texture2dRegion AddRegion(Bitmap bitmap, string description)
    {
        if (bitmap.Width > width || bitmap.Height > height)
        {
            Trace.TraceWarning($"Bitmap \"{description}\" doesn't fit in this atlas");

            var texture = Texture2d.Load(bitmap, description, textureOptions);
            (oversizeTextures ??= []).Add(texture);
            return texture;
        }

        var atlas = atlases[^1];
        var region = atlas.AddRegion(bitmap);

        if (region is null)
        {
            Trace.WriteLine($"{this.description} full, adding an atlas");
            atlas = pushAtlas();
            region = atlas.AddRegion(bitmap);
        }

        return region;
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