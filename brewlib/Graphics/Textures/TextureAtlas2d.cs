namespace BrewLib.Graphics.Textures;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public sealed class TextureAtlas2d(int width,
    int height,
    string description,
    TextureOptions textureOptions = null,
    int padding = 0) : IDisposable
{
    readonly List<Rectangle> _freeRegions = [new(0, 0, width, height)];
    readonly Texture2d texture = Texture2d.Create(default, description, width, height, textureOptions);

    public Texture2dRegion AddRegion(Image<Rgba32> bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var shouldUpscale = width < 2 || height < 2;

        if (shouldUpscale)
        {
            width *= 3;
            height *= 3;
        }

        width += padding;
        height += padding;

        for (var i = 0; i < _freeRegions.Count; ++i)
        {
            var free = _freeRegions[i];
            if (free.Width < width || free.Height < height) continue;

            Texture2dAtlasRegion region = new(texture, new(free.X, free.Y, bitmap.Width, bitmap.Height), this);
            if (shouldUpscale)
            {
                bitmap.Mutate(x => x.Resize(width, height, KnownResamplers.NearestNeighbor));
                region.WasUpscaled = true;
            }

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

        if (region.WasUpscaled)
        {
            width *= 3;
            height *= 3;
        }

        texture.Update(default, region.X, region.Y, width, height);

        width += padding;
        height += padding;

        _freeRegions.Add(new(region.X, region.Y, width, height));
        MergeRectangles();
    }

    void MergeRectangles()
    {
        int maxX = 0, maxY = 0;
        foreach (var r in _freeRegions)
        {
            maxX = Math.Max(maxX, r.Right);
            maxY = Math.Max(maxY, r.Bottom);
        }

        var grid = Span2D<bool>.DangerousCreate(ref MemoryMarshal.GetReference(stackalloc bool[maxX * maxY]), maxX, maxY, 0);
        foreach (var r in _freeRegions)
            for (var x = r.X; x < r.Right; x++)
            for (var y = r.Y; y < r.Bottom; y++)
                grid[x, y] = true;

        _freeRegions.Clear();

        for (var y = 0; y < maxY; ++y)
        for (var x = 0; x < maxX; ++x)
        {
            if (!grid[x, y]) continue;

            var width = 0;
            while (x + width < maxX && grid[x + width, y]) width++;

            var height = 1;
            var stop = false;
            while (y + height < maxY && !stop)
            {
                for (var dx = 0; dx < width; dx++)
                    if (!grid[x + dx, y + height])
                    {
                        stop = true;
                        break;
                    }

                if (!stop) ++height;
            }

            for (var dx = 0; dx < width; ++dx)
            for (var dy = 0; dy < height; ++dy)
                grid[x + dx, y + dy] = false;

            _freeRegions.Add(new(x, y, width, height));
        }
    }

    class Texture2dAtlasRegion(Texture2d texture, Rectangle bounds, TextureAtlas2d parent) : Texture2dRegion(texture, bounds)
    {
        public bool WasUpscaled;

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
        disposed = true;
    }

    #endregion
}