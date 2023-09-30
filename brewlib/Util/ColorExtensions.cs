﻿using OpenTK;
using OpenTK.Graphics;
using System;

namespace BrewLib.Util
{
    public static class ColorExtensions
    {
        public static int ToRgba(this Color4 color)
            => ((byte)(color.A * 255) << 24) | ((byte)(color.B * 255) << 16) | ((byte)(color.G * 255) << 8) | (byte)(color.R * 255);

        public static Color4 ToColor4(this int color) => new Color4(
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

        public static Vector4 ToHsba(this Color4 color)
        {
            float r = color.R, g = color.G, b = color.B;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;

            var hue = 0f;
            if (r == max) hue = (g - b) / delta;
            else if (g == max) hue = 2 + (b - r) / delta;
            else if (b == max) hue = 4 + (r - g) / delta;
            hue /= 6;
            if (hue < 0f) hue += 1f;

            var saturation = (max <= 0) ? 0 : 1f - (min / max);

            return new Vector4(hue, saturation, max, color.A);
        }
        public static Color4 WithOpacity(this Color4 color, float opacity)
            => new Color4(color.R, color.G, color.B, color.A * opacity);

        public static Color4 Premultiply(this Color4 color) => new Color4(color.R * color.A, color.G * color.A, color.B * color.A, color.A);
    }
}