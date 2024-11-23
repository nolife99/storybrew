namespace StorybrewCommon.Subtitles;

using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Processing;

#pragma warning disable CS1591
public interface FontEffect
{
    ///<summary> Whether to overlay the effect over the original texture. </summary>
    bool Overlay { get; }

    ///<summary> The vector radius of the font effect. </summary>
    SizeF Measure { get; }

    ///<summary> Draws the font effect over the texture. </summary>
    void Draw(IImageProcessingContext bitmap, IPathCollection path, float x, float y);
}

/// <summary> Stores information about a font's appearance. </summary>
/// <remarks> Creates a new <see cref="FontDescription"/> storing a descriptor for <see cref="FontGenerator"/>. </remarks>
/// <param name="FontPath"> The path to the font file. </param>
/// <param name="FontSize"> The relative size of the font. </param>
/// <param name="Color"> The coloring tint of the font. </param>
/// <param name="Padding"> Allocate extra space around the font when generating it. </param>
/// <param name="FontStyle"> Format/style of the font. </param>
/// <param name="TrimTransparency"> Trim transparent space around the font. </param>
/// <param name="EffectsOnly"> Leave out the original font and keep the effects. </param>
/// <param name="Debug"> Draw a randomly colored background behind the font. </param>
public record FontDescription(string FontPath,
    int FontSize = 76,
    Color Color = default,
    Vector2 Padding = default,
    FontStyle FontStyle = default,
    bool TrimTransparency = true,
    bool EffectsOnly = false,
    bool Debug = false);