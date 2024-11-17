namespace BrewLib.Util;

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

public static class ColorExtensions
{
    public static Rgba32 LerpColor(this Rgba32 color, Rgba32 otherColor, float blend)
    {
        var invBlend = 1 - blend;
        return new((byte)(color.R * invBlend + otherColor.R * blend),
            (byte)(color.G * invBlend + otherColor.G * blend), (byte)(color.B * invBlend + otherColor.B * blend), color.A);
    }
    public static Rgba32 WithOpacity(this Rgba32 color, float opacity)
        => new(color.R, color.G, color.B, (byte)(color.A * opacity));

    public static Rgba32 FromHsb(Vector4 hsba)
    {
        float hue = hsba.X * 360, saturation = hsba.Y, brightness = hsba.Z;
        var c = brightness * saturation;

        var h = hue / 60;
        var x = c * (1 - float.Abs(h % 2 - 1));

        float r, g, b;
        switch (h)
        {
            case >= 0 and < 1:
                r = c;
                g = x;
                b = 0;
                break;
            case >= 1 and < 2:
                r = x;
                g = c;
                b = 0;
                break;
            case >= 2 and < 3:
                r = 0;
                g = c;
                b = x;
                break;
            case >= 3 and < 4:
                r = 0;
                g = x;
                b = c;
                break;
            case >= 4 and < 5:
                r = x;
                g = 0;
                b = c;
                break;
            case >= 5 and < 6:
                r = c;
                g = 0;
                b = x;
                break;
            default:
                r = 0;
                g = 0;
                b = 0;
                break;
        }

        var m = brightness - c;
        return new(r + m, g + m, b + m, hsba.W);
    }

    public static Vector4 ToHsb(Rgba32 rgb)
    {
        var vec = rgb.ToVector4();

        var max = float.Max(vec.X, float.Max(vec.Y, vec.Z));
        var min = float.Min(vec.X, float.Min(vec.Y, vec.Z));
        var delta = max - min;

        var hue = 0f;
        if (vec.X == max) hue = (vec.Y - vec.Z) / delta;
        else if (vec.Y == max) hue = 2 + (vec.Z - vec.X) / delta;
        else if (vec.Z == max) hue = 4 + (vec.X - vec.Y) / delta;

        hue /= 6;
        if (hue < 0) ++hue;

        var saturation = float.IsNegative(max) ? 0 : 1 - min / max;
        return new(hue, saturation, max, vec.W);
    }
}