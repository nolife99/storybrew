﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace BrewLib.Graphics.Textures;

public sealed class TextureMultiAtlas2d : IDisposable
{
    List<TextureAtlas2d> atlases = [];
    List<Texture2d> oversizeTextures;

    readonly int width, height, padding;
    readonly string description;
    readonly TextureOptions textureOptions;

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
            Trace.WriteLine($"Bitmap \"{description}\" doesn't fit in this atlas");

            var texture = Texture2d.Load(bitmap, description, textureOptions);
            (oversizeTextures ??= []).Add(texture);
            return texture;
        }

        var atlas = atlases[^1];
        var region = atlas.AddRegion(bitmap, description);

        if (region is null)
        {
            Trace.WriteLine($"{this.description} is full, adding an atlas");
            atlas = pushAtlas();
            region = atlas.AddRegion(bitmap, description);
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
        if (!disposed)
        {
            foreach (var atlas in atlases) atlas.Dispose();
            atlases.Clear();

            if (oversizeTextures is not null)
            {
                foreach (var texture in oversizeTextures) texture.Dispose();
                oversizeTextures.Clear();
                oversizeTextures = null;
            }

            atlases = null;
            disposed = true;
        }
    }

    #endregion
}