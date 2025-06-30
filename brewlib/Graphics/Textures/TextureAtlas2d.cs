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
    readonly PooledList<Rectangle> freeRegions = [new(0, 0, width, height)];
    readonly Texture2d texture = Texture2d.Create(Color.Transparent.ToPixel<Rgba32>(), width, height, textureOptions);

    bool wasMerged;

    public Texture2dRegion AddRegion(Image<Rgba32> bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        width += padding;
        height += padding;

        MergeRectangles();

        for (var i = 0; i < freeRegions.Count; ++i)
        {
            var free = freeRegions[i];
            if (free.Width < width || free.Height < height) continue;

            Texture2dAtlasRegion region = new(texture, new(free.X, free.Y, bitmap.Width, bitmap.Height), this);
            texture.Update(bitmap, free.X, free.Y);

            freeRegions.RemoveAt(i);

            if (free.Width > width)
            {
                freeRegions.Add(new(free.X + width, free.Y, free.Width - width, height));
                wasMerged = false;
            }

            if (free.Height > height)
            {
                freeRegions.Add(new(free.X, free.Y + height, free.Width, free.Height - height));
                wasMerged = false;
            }

            return region;
        }

        return null;
    }

    void FreeRegion(Texture2dAtlasRegion region)
    {
        if (disposed) return;

        var width = region.Width;
        var height = region.Height;

        texture.Update(Color.Transparent.ToPixel<Rgba32>(), region.X, region.Y, width, height);

        width += padding;
        height += padding;

        freeRegions.Add(new(region.X, region.Y, width, height));
        wasMerged = false;
    }

    void MergeRectangles()
    {
        if (freeRegions.Count < 2 || wasMerged) return;

        freeRegions.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));

        bool merged;
        do
        {
            merged = false;
            for (var i = 0; i < freeRegions.Count; i++)
            {
                for (var j = i + 1; j < freeRegions.Count; j++)
                {
                    var r1 = freeRegions[i];
                    var r2 = freeRegions[j];

                    if (r1.Y == r2.Y && r1.Height == r2.Height && r1.Right == r2.X)
                    {
                        freeRegions[i] = new(r1.X, r1.Y, r1.Width + r2.Width, r1.Height);
                        merged = true;
                    }
                    else if (r1.X == r2.X && r1.Width == r2.Width && r1.Bottom == r2.Y)
                    {
                        freeRegions[i] = new(r1.X, r1.Y, r1.Width, r1.Height + r2.Height);
                        merged = true;
                    }

                    if (!merged) continue;

                    freeRegions.RemoveAt(j);
                    break;
                }

                if (merged) break;
            }
        }
        while (merged);

        wasMerged = true;
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
        freeRegions.Dispose();

        disposed = true;
    }

    #endregion
}