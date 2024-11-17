namespace BrewLib.Util;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

public static class BitmapHelper
{
    public static bool IsFullyTransparent(Image<Rgba32> source)
    {
        for (var y = 0; y < source.Height; y++)
        {
            var row = source.DangerousGetPixelRowMemory(y).Span;
            foreach (var pixel in row) if (pixel.A != 0) return false;
        }

        return true;
    }

    public static Rectangle FindTransparencyBounds(Image<Rgba32> source)
    {
        var size = source.Size;
        int xMin = size.Width, yMin = size.Height, xMax = -1, yMax = -1, width = size.Width, height = size.Height;

        source.ProcessPixelRows(src =>
        {
            for (var y = 0; y < height; ++y)
            {
                var srcData = src.GetRowSpan(y);
                for (var x = 0; x < width; ++x)
                    if (srcData[x].A != 0)
                    {
                        if (x < xMin) xMin = x;
                        if (x > xMax) xMax = x;
                        if (y < yMin) yMin = y;
                        if (y > yMax) yMax = y;
                    }
            }
        });

        return xMin <= xMax && yMin <= yMax ? Rectangle.FromLTRB(xMin, yMin, xMax + 1, yMax + 1) : default;
    }
}