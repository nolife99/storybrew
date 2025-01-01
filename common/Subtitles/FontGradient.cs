namespace StorybrewCommon.Subtitles;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

/// <summary> A font gradient effect. </summary>
/// <remarks> Creates a new <see cref="FontGradient"/> with a descriptor about a smooth gradient effect. </remarks>
/// <param name="offset"> The gradient offset of the effect. </param>
/// <param name="size"> The relative size of the gradient. </param>
/// <param name="color"> The color tinting of the gradient. </param>
/// <param name="wrapMode"> How the gradient is tiled when it is smaller than the area being filled. </param>
public record FontGradient(PointF offset = default,
    SizeF size = default,
    Color color = default,
    GradientRepetitionMode wrapMode = GradientRepetitionMode.Reflect) : FontEffect
{
    readonly LinearGradientBrush brush = new(
        new(offset.X, offset.Y),
        new(offset.X + size.Width, offset.Y + size.Height),
        wrapMode,
        new(0, color),
        new(1, color.WithAlpha(0)));

    /// <inheritdoc/>
    public bool Overlay => true;

    /// <inheritdoc/>
    public SizeF Measure => default;

    /// <inheritdoc/>
    public void Draw(IImageProcessingContext bitmap, IPathCollection path, float x, float y)
        => bitmap.Fill(FontGenerator.options, brush, path);
}