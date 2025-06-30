namespace StorybrewCommon.Subtitles;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

/// <summary> A font background effect. </summary>
public record FontBackground : FontEffect
{
    readonly SolidBrush brush;

    /// <summary> Creates a new <see cref="FontBackground"/> descriptor with information about a font background. </summary>
    /// <param name="color"> The coloring tint of the glow. </param>
    public FontBackground(Color color = default) => brush = new(color);

    /// <inheritdoc/>
    public bool Overlay => false;

    /// <inheritdoc/>
    public SizeF Measure => SizeF.Empty;

    /// <inheritdoc/>
    public void Draw(IImageProcessingContext bitmap, IPathCollection path, float x, float y) => bitmap.Clear(brush);
}