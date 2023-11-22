using osuTK.Graphics;

namespace BrewLib.Util
{
    public static class ColorExtensions
    {
        public static int ToRgba(this Color4 color)
            => ((byte)(color.A * 255) << 24) | ((byte)(color.B * 255) << 16) | ((byte)(color.G * 255) << 8) | (byte)(color.R * 255);

        public static Color4 ToColor4(this int color) => new(
            (byte)(color & 0xFF),
            (byte)((color >> 8) & 0xFF),
            (byte)((color >> 16) & 0xFF),
            (byte)(color >> 24));

        public static Color4 LerpColor(this Color4 color, Color4 otherColor, float blend)
        {
            var invBlend = 1 - blend;
            return new Color4(
                color.R * invBlend + otherColor.R * blend,
                color.G * invBlend + otherColor.G * blend,
                color.B * invBlend + otherColor.B * blend,
                color.A);
        }

        public static Color4 WithOpacity(this Color4 color, float opacity)
            => new(color.R, color.G, color.B, color.A * opacity);

        public static Color4 Premultiply(this Color4 color) => new(color.R * color.A, color.G * color.A, color.B * color.A, color.A);
    }
}