namespace BrewLib.Util;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public static class BitmapHelper
{
    public static bool IsFullyTransparent(Image<Rgba32> source)
    {
        var buffer = source.Frames.RootFrame.PixelBuffer;
        for (var y = 0; y < source.Height; ++y)
            foreach (var pixel in buffer.DangerousGetRowSpan(y))
                if (pixel.A != 0)
                    return false;

        return true;
    }

    public static Rectangle FindTransparencyBounds(Image<Rgba32> source)
    {
        int xMin = source.Width, yMin = source.Height, xMax = -1, yMax = -1, width = source.Width, height = source.Height;

        var buffer = source.Frames.RootFrame.PixelBuffer;
        for (var y = 0; y < height; ++y)
        {
            var srcData = buffer.DangerousGetRowSpan(y);
            for (var x = 0; x < width; ++x)
                if (srcData[x].A != 0)
                {
                    if (x < xMin) xMin = x;
                    if (x > xMax) xMax = x;
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;
                }
        }

        return xMin <= xMax && yMin <= yMax ? Rectangle.FromLTRB(xMin, yMin, xMax + 1, yMax + 1) : default;
    }
}