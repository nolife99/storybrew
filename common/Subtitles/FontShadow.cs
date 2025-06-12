namespace StorybrewCommon.Subtitles;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

/// <summary> A font drop shadow effect. </summary>
public sealed record FontShadow : FontEffect
{
    readonly SolidBrush brush;
    readonly int thickness;

    /// <summary> Creates a new <see cref="FontShadow"/>. </summary>
    /// <param name="thickness"> The thickness of the shadow. </param>
    /// <param name="color"> The color of the shadow. </param>
    public FontShadow(int thickness = 1, Color color = default)
    {
        this.thickness = thickness;
        brush = new(color);
    }

    /// <inheritdoc/>
    public bool Overlay => false;

    /// <inheritdoc/>
    public SizeF Measure => new(thickness * 2, thickness * 2);

    /// <inheritdoc/>
    public void Draw(IImageProcessingContext bitmap, IPathCollection path, float x, float y)
    {
        if (thickness < 1) return;

        bitmap.Fill(FontGenerator.options, brush, path.Translate(thickness, thickness));
    }
}