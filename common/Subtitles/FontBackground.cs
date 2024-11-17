namespace StorybrewCommon.Subtitles;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

/// <summary> A font background effect. </summary>
/// <remarks> Creates a new <see cref="FontBackground"/> descriptor with information about a font background. </remarks>
/// <param name="color"> The coloring tint of the glow. </param>
public class FontBackground(Rgba32 color = default) : FontEffect
{
    ///<summary> The coloring tint of the glow. </summary>
    public Rgba32 Color => color;

    /// <inheritdoc/>
    public bool Overlay => false;

    /// <inheritdoc/>
    public SizeF Measure => default;

    /// <inheritdoc/>
    public void Draw(Image<Rgba32> bitmap, IPathCollection path, float x, float y)
        => bitmap.Mutate(b => b.Clear(Color));
}