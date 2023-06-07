using BrewLib.Util;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

namespace StorybrewCommon.Subtitles
{
    ///<summary> A font glow effect. </summary>
    public class FontGlow : FontEffect
    {
        double[,] kernel;

        int radius = 6;
        ///<summary> Gets or sets the radius of the glow. </summary>
        public int Radius
        {
            get => radius;
            set
            {
                if (radius == value) return;
                radius = value;
                kernel = null;
            }
        }

        double power = 0;
        ///<summary> Gets or sets the intensity of the glow. </summary>
        public double Power
        {
            get => power;
            set
            {
                if (power == value) return;
                power = value;
                kernel = null;
            }
        }

        ///<summary> The coloring tint of the glow. </summary>
        public FontColor Color = FontColor.FromRgba(255, 255, 255, 100);

        ///<inheritdoc/>
        public bool Overlay => false;

        ///<inheritdoc/>
        public Vector2 Measure() => new Vector2(Radius * 2);

        ///<inheritdoc/>
        public void Draw(Bitmap bitmap, Graphics textGraphics, Font font, StringFormat stringFormat, string text, float x, float y)
        {
            if (Radius < 1) return;

            using (var blurSource = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb))
            {
                using (var brush = new SolidBrush(FontColor.White)) using (var graphics = Graphics.FromImage(blurSource))
                {
                    graphics.TextRenderingHint = textGraphics.TextRenderingHint;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawString(text, font, brush, x, y, stringFormat);
                }

                if (kernel == null)
                {
                    var radius = Math.Min(Radius, 24);
                    var power = Power >= 1 ? Power : Radius * .5;
                    kernel = BitmapHelper.CalculateGaussianKernel(radius, power);
                }

                using (var blurredBitmap = BitmapHelper.ConvoluteAlpha(blurSource, kernel, Color))
                    textGraphics.DrawImage(blurredBitmap.Bitmap, 0, 0);
            }
        }
    }
}