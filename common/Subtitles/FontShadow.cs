namespace StorybrewCommon.Subtitles;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

/// <summary> A font drop shadow effect. </summary>
/// <remarks> Creates a new <see cref="FontShadow"/> descriptor with information about a drop shadow effect. </remarks>
/// <param name="thickness"> The thickness of the shadow. </param>
/// <param name="color"> The color tinting of the shadow. </param>
public record FontShadow(int thickness = 1, Color color = default) : FontEffect
{
    readonly SolidBrush brush = new(color);

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