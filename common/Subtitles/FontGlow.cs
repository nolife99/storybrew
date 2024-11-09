namespace StorybrewCommon.Subtitles;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using BrewLib.Util;

/// <summary> A font glow effect. </summary>
/// <remarks> Creates a new <see cref="FontGlow"/> descriptor with information about a Gaussian blur effect. </remarks>
/// <param name="radius"> The radius of the glow. </param>
/// <param name="power"> The intensity of the glow. </param>
/// <param name="color"> The coloring tint of the glow. </param>
public class FontGlow(int radius = 6, float power = 0, FontColor color = default) : FontEffect
{
    ///<summary> The radius of the glow. </summary>
    public int Radius => radius;

    ///<summary> The intensity of the glow. </summary>
    public float Power => power;

    ///<summary> The coloring tint of the glow. </summary>
    public FontColor Color => color;

    /// <inheritdoc/>
    public bool Overlay => false;

    /// <inheritdoc/>
    public SizeF Measure => new(Radius * 2, Radius * 2);

    /// <inheritdoc/>
    public void Draw(Bitmap bitmap, Graphics textGraphics, GraphicsPath path, float x, float y)
    {
        if (Radius < 1) return;

        using Bitmap src = new(bitmap.Width, bitmap.Height, bitmap.PixelFormat);
        using (var graphics = Graphics.FromImage(src))
        {
            graphics.InterpolationMode = InterpolationMode.Bilinear;
            graphics.FillPath(Brushes.White, path);
        }

        using var blur = BitmapHelper.BlurAlpha(src, Math.Min(Radius, 24), Power >= 1 ? Power : Radius * .5f, Color);
        textGraphics.DrawImage(blur.Bitmap, 0, 0, bitmap.Width, bitmap.Height);
    }
}