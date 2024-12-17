namespace StorybrewCommon.Subtitles;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

/// <summary> A font background effect. </summary>
/// <remarks> Creates a new <see cref="FontBackground"/> descriptor with information about a font background. </remarks>
/// <param name="color"> The coloring tint of the glow. </param>
public record FontBackground(Color color = default) : FontEffect
{
    readonly SolidBrush brush = new(color);

    /// <inheritdoc/>
    public bool Overlay => false;

    /// <inheritdoc/>
    public SizeF Measure => default;

    /// <inheritdoc/>
    public void Draw(IImageProcessingContext bitmap, IPathCollection path, float x, float y) => bitmap.Clear(brush);
}