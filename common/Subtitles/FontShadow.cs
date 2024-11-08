namespace StorybrewCommon.Subtitles;

using System.Drawing;
using System.Drawing.Drawing2D;

/// <summary> A font drop shadow effect. </summary>
/// <remarks> Creates a new <see cref="FontShadow" /> descriptor with information about a drop shadow effect. </remarks>
/// <param name="thickness"> The thickness of the shadow. </param>
/// <param name="color"> The color tinting of the shadow. </param>
public class FontShadow(int thickness = 1, FontColor color = default) : FontEffect
{
    ///<summary> The thickness of the shadow. </summary>
    public int Thickness => thickness;

    ///<summary> The color tinting of the shadow. </summary>
    public FontColor Color => color;

    /// <inheritdoc />
    public bool Overlay => false;

    /// <inheritdoc />
    public SizeF Measure => new(Thickness * 2, Thickness * 2);

    /// <inheritdoc />
    public void Draw(Bitmap bitmap, Graphics textGraphics, GraphicsPath path, float x, float y)
    {
        if (Thickness < 1) return;

        using var transformed = (GraphicsPath)path.Clone();
        using (Matrix translate = new())
        {
            translate.Translate(thickness, thickness);
            transformed.Transform(translate);
        }

        using SolidBrush brush = new(Color);
        textGraphics.FillPath(brush, transformed);
    }
}