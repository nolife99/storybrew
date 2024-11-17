namespace StorybrewCommon.Subtitles;

using System;
using BrewLib.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

/// <summary> A font glow effect. </summary>
/// <remarks> Creates a new <see cref="FontGlow"/> descriptor with information about a Gaussian blur effect. </remarks>
/// <param name="radius"> The radius of the glow. </param>
/// <param name="power"> The intensity of the glow. </param>
/// <param name="color"> The coloring tint of the glow. </param>
public class FontGlow(int radius = 6, float power = 0, Rgba32 color = default) : FontEffect
{
    ///<summary> The radius of the glow. </summary>
    public int Radius => radius;

    ///<summary> The intensity of the glow. </summary>
    public float Power => power;

    ///<summary> The coloring tint of the glow. </summary>
    public Rgba32 Color => color;

    /// <inheritdoc/>
    public bool Overlay => false;

    /// <inheritdoc/>
    public SizeF Measure => new(Radius * 2, Radius * 2);

    /// <inheritdoc/>
    public void Draw(Image<Rgba32> bitmap, IPathCollection path, float x, float y)
    {
        if (Radius < 1) return;

        bitmap.Mutate(b =>
        {
            b.Fill(FontGenerator.options, Color, path);
            b.ApplyProcessor(new GaussianBlurProcessor(Power >= 1 ? Power : Radius * .5f, Math.Min(Radius, 24)));
        });
    }
}