namespace StorybrewCommon.Subtitles;

using BrewLib.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

/// <summary> A font gradient effect. </summary>
/// <remarks> Creates a new <see cref="FontGradient"/> with a descriptor about a smooth gradient effect. </remarks>
/// <param name="offset"> The gradient offset of the effect. </param>
/// <param name="size"> The relative size of the gradient. </param>
/// <param name="color"> The color tinting of the gradient. </param>
/// <param name="wrapMode"> How the gradient is tiled when it is smaller than the area being filled. </param>
public class FontGradient(PointF offset = default,
    SizeF size = default,
    Rgba32 color = default,
    GradientRepetitionMode wrapMode = GradientRepetitionMode.Reflect) : FontEffect
{
    ///<summary> The gradient offset of the effect. </summary>
    public PointF Offset => offset;

    ///<summary> The relative size of the gradient. </summary>
    public SizeF Size => size;

    ///<summary> The color tinting of the gradient. </summary>
    public Rgba32 Color => color;

    ///<summary> How the gradient is tiled when it is smaller than the area being filled. </summary>
    public GradientRepetitionMode WrapMode => wrapMode;

    /// <inheritdoc/>
    public bool Overlay => true;

    /// <inheritdoc/>
    public SizeF Measure => default;

    /// <inheritdoc/>
    public void Draw(Image<Rgba32> bitmap, IPathCollection path, float x, float y) => bitmap.Mutate(b
        => b.Fill(FontGenerator.options, new LinearGradientBrush(new PointF(x + Offset.X, y + Offset.Y),
            new(x + Offset.X + Size.Width, y + Offset.Y + Size.Height), wrapMode, new ColorStop(0, Color),
            new ColorStop(1, Color.WithOpacity(0))), path));
}