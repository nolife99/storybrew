﻿namespace StorybrewCommon.Subtitles;

using System.Drawing;
using System.Drawing.Drawing2D;

/// <summary> A font gradient effect. </summary>
/// <remarks> Creates a new <see cref="FontGradient" /> with a descriptor about a smooth gradient effect. </remarks>
/// <param name="offset"> The gradient offset of the effect. </param>
/// <param name="size"> The relative size of the gradient. </param>
/// <param name="color"> The color tinting of the gradient. </param>
/// <param name="wrapMode"> How the gradient is tiled when it is smaller than the area being filled. </param>
public class FontGradient(
    PointF offset = default, SizeF size = default, FontColor color = default, WrapMode wrapMode = WrapMode.TileFlipXY)
    : FontEffect
{
    ///<summary> The gradient offset of the effect. </summary>
    public PointF Offset => offset;

    ///<summary> The relative size of the gradient. </summary>
    public SizeF Size => size;

    ///<summary> The color tinting of the gradient. </summary>
    public FontColor Color => color;

    ///<summary> How the gradient is tiled when it is smaller than the area being filled. </summary>
    public WrapMode WrapMode => wrapMode;

    /// <inheritdoc />
    public bool Overlay => true;

    /// <inheritdoc />
    public SizeF Measure => default;

    /// <inheritdoc />
    public void Draw(Bitmap bitmap, Graphics textGraphics, GraphicsPath path, float x, float y)
    {
        using LinearGradientBrush brush =
            new(new PointF(x + Offset.X, y + Offset.Y), new(x + Offset.X + Size.Width, y + Offset.Y + Size.Height),
                Color, System.Drawing.Color.FromArgb(0, Color.R, Color.G, Color.B)) { WrapMode = WrapMode };
        textGraphics.FillPath(brush, path);
    }
}