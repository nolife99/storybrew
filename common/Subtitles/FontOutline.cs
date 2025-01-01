namespace StorybrewCommon.Subtitles;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

/// <summary> A font outline effect. </summary>
/// <remarks> Creates a new <see cref="FontOutline"/> descriptor with information about an outlining effect. </remarks>
/// <param name="thickness"> The thickness of the outline. </param>
/// <param name="color"> The color of the outline. </param>
public record FontOutline(int thickness = 1, Color color = default) : FontEffect
{
    const float diagonal = 1.41421356237f * 2;

    readonly SolidPen pen = new(color, thickness);

    /// <inheritdoc/>
    public bool Overlay => false;

    /// <inheritdoc/>
    public SizeF Measure => new(thickness * diagonal, thickness * diagonal);

    /// <inheritdoc/>
    public void Draw(IImageProcessingContext bitmap, IPathCollection path, float x, float y)
    {
        if (thickness < 1) return;

        bitmap.Draw(FontGenerator.options, pen, path);
    }
}