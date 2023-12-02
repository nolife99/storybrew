using System.Drawing;

namespace BrewLib.Util;

public static class ColorExtensions
{
    public static int ToRgba(this Color color) => (color.A << 24) | (color.B << 16) | (color.G << 8) | color.R;
    public static Color LerpColor(this Color color, Color otherColor, float blend)
    {
        var invBlend = 1 - blend;
        return Color.FromArgb(color.A,
            (int)(color.R * invBlend + otherColor.R * blend),
            (int)(color.G * invBlend + otherColor.G * blend),
            (int)(color.B * invBlend + otherColor.B * blend));
    }
    public static Color WithOpacity(this Color color, float opacity) => Color.FromArgb((int)(color.A * opacity), color.R, color.G, color.B);
}