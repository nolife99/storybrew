using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace BrewLib.Graphics.Textures;

public sealed class TextureMultiAtlas2d : IDisposable
{
    Stack<TextureAtlas2d> atlases = new();
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

        var atlas = atlases.Peek();
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
        TextureAtlas2d atlas = new(width, height, $"{description} #{atlases.Count + 1}", textureOptions, padding);
        atlases.Push(atlas);
        return atlas;
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            while (atlases.Count > 0) atlases.Pop().Dispose();
            oversizeTextures?.ForEach(texture => texture.Dispose());
            oversizeTextures?.Clear();

            atlases = null;
            oversizeTextures = null;
            disposed = true;
        }
    }

    #endregion
}