﻿using BrewLib.Util;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace StorybrewCommon.Subtitles
{
    ///<summary> A font glow effect. </summary>
    public class FontGlow : FontEffect
    {
        ///<summary> The radius of the glow. </summary>
        public readonly int Radius;

        ///<summary> The intensity of the glow. </summary>
        public readonly double Power;

        ///<summary> The coloring tint of the glow. </summary>
        public readonly FontColor Color;

        ///<inheritdoc/>
        public bool Overlay => false;

        ///<inheritdoc/>
        public SizeF Measure => new Size(Radius * 2, Radius * 2);

        ///<summary> Creates a new <see cref="FontGlow"/> descriptor with information about a Gaussian blur effect. </summary>
        ///<param name="radius"> The radius of the glow. </param>
        ///<param name="power"> The intensity of the glow. </param>
        ///<param name="color"> The coloring tint of the glow. </param>
        public FontGlow(int radius = 6, double power = 0, FontColor color = default)
        {
            Radius = radius;
            Power = power;
            Color = color;
        }

        ///<inheritdoc/>
        public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y)
        {
            if (Radius < 1) return;

            using var src = new Bitmap(bitmap.Width, bitmap.Height, bitmap.PixelFormat);
            using (var brush = new SolidBrush(FontColor.White)) using (var graphics = Graphics.FromImage(src))
            {
                graphics.TextRenderingHint = textGraphics.TextRenderingHint;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawString(text, font, brush, x, y, stringFormat);
            }

            using var blur = BitmapHelper.BlurAlpha(src, Math.Min(Radius, 24), Power >= 1 ? Power : Radius * .5, Color); textGraphics.DrawImage(blur.Bitmap, 0, 0);
        }
    }
}