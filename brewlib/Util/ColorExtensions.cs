namespace BrewLib.Util;

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public static class ColorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color LerpColor(this Color color, ref readonly Color otherColor, float blend)
    {
        var rgba = (Vector4)color;
        return new Rgba64(Vector4.Lerp(rgba, (Vector4)otherColor, blend) with { Z = rgba.Z });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color WithOpacity(this Color color, float opacity)
    {
        var rgba = (Vector4)color;
        return new Rgba64(rgba with { W = rgba.W * opacity });
    }

    public static Vector4 FromHsb(Vector4 hsba)
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
        return new Vector4(r + m, g + m, b + m, hsba.W);
    }

    public static Vector4 ToHsb(Vector4 vec)
    {
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