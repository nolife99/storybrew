namespace StorybrewCommon.Subtitles;

using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary> A font glow effect. </summary>
/// <remarks> Creates a new <see cref="FontGlow"/> descriptor with information about a Gaussian blur effect. </remarks>
/// <param name="radius"> The radius of the glow. </param>
/// <param name="power"> The intensity of the glow. </param>
/// <param name="color"> The coloring tint of the glow. </param>
public record FontGlow(int radius = 6, float power = 0, Color color = default) : FontEffect
{
    readonly GaussianBlurProcessor blur = new(power >= 1 ? power : radius * .5f, Math.Min(radius, 24));

    /// <inheritdoc/>
    public bool Overlay => false;

    /// <inheritdoc/>
    public SizeF Measure => new(radius * 2, radius * 2);

    /// <inheritdoc/>
    public void Draw(IImageProcessingContext bitmap, IPathCollection path, float x, float y)
    {
        if (radius < 1) return;

        bitmap.Fill(FontGenerator.options, color, path).ApplyProcessor(blur);
    }
}