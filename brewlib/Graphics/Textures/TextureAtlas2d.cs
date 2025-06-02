namespace BrewLib.Graphics.Textures;

using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tiny.PooledCollections.Generic;

public sealed class TextureAtlas2d(int width,
    int height,
    TextureOptions textureOptions = null,
    int padding = 0) : IDisposable
{
    readonly PooledList<Rectangle> _freeRegions = [new(0, 0, width, height)];
    readonly Texture2d texture = Texture2d.Create(default, width, height, textureOptions);

    public Texture2dRegion AddRegion(Image<Rgba32> bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        width += padding;
        height += padding;

        for (var i = 0; i < _freeRegions.Count; ++i)
        {
            var free = _freeRegions[i];
            if (free.Width < width || free.Height < height) continue;

            Texture2dAtlasRegion region = new(texture, new(free.X, free.Y, bitmap.Width, bitmap.Height), this);
            texture.Update(bitmap, free.X, free.Y);

            _freeRegions.RemoveAt(i);

            if (free.Width > width) _freeRegions.Add(new(free.X + width, free.Y, free.Width - width, height));
            if (free.Height > height) _freeRegions.Add(new(free.X, free.Y + height, free.Width, free.Height - height));

            MergeRectangles();

            return region;
        }

        return null;
    }

    void FreeRegion(Texture2dAtlasRegion region)
    {
        if (disposed) return;

        var width = region.Width;
        var height = region.Height;

        texture.Update(default, region.X, region.Y, width, height);

        width += padding;
        height += padding;

        _freeRegions.Add(new(region.X, region.Y, width, height));
        MergeRectangles();
    }

    void MergeRectangles()
    {
        if (_freeRegions.Count <= 1) return;

        _freeRegions.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

        bool merged;
        do
        {
            merged = false;
            for (var i = 0; i < _freeRegions.Count; i++)
            {
                for (var j = i + 1; j < _freeRegions.Count; j++)
                {
                    var r1 = _freeRegions[i];
                    var r2 = _freeRegions[j];

                    if (r1.Y == r2.Y && r1.Height == r2.Height && r1.Right == r2.X)
                    {
                        _freeRegions[i] = new(r1.X, r1.Y, r1.Width + r2.Width, r1.Height);
                        merged = true;
                    }
                    else if (r1.X == r2.X && r1.Width == r2.Width && r1.Bottom == r2.Y)
                    {
                        _freeRegions[i] = new(r1.X, r1.Y, r1.Width, r1.Height + r2.Height);
                        merged = true;
                    }

                    if (!merged) continue;

                    _freeRegions.RemoveAt(j);
                    break;
                }

                if (merged) break;
            }
        }
        while (merged);
    }

    class Texture2dAtlasRegion(Texture2d texture, Rectangle bounds, TextureAtlas2d parent) : Texture2dRegion(texture, bounds)
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            parent.FreeRegion(this);
        }
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        texture.Dispose();
        _freeRegions.Dispose();

        disposed = true;
    }

    #endregion
}