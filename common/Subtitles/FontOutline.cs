namespace StorybrewCommon.Subtitles;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

/// <summary> A font outline effect. </summary>
/// <remarks> Creates a new <see cref="FontOutline"/> descriptor with information about an outlining effect. </remarks>
/// <param name="thickness"> The thickness of the outline. </param>
/// <param name="color"> The color of the outline. </param>
public class FontOutline(int thickness = 1, Rgba32 color = default) : FontEffect
{
    const float diagonal = 1.41421356237f;

    ///<summary> The thickness of the outline. </summary>
    public int Thickness => thickness;

    ///<summary> The color of the outline. </summary>
    public Rgba32 Color => color;

    /// <inheritdoc/>
    public bool Overlay => false;

    /// <inheritdoc/>
    public SizeF Measure => new(Thickness * diagonal * 2, Thickness * diagonal * 2);

    /// <inheritdoc/>
    public void Draw(Image<Rgba32> bitmap, IPathCollection path, float x, float y)
    {
        if (Thickness < 1) return;
        bitmap.Mutate(b => b.Draw(FontGenerator.options, new SolidPen(Color, thickness), path));
    }
}