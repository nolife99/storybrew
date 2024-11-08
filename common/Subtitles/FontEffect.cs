namespace StorybrewCommon.Subtitles;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;

#pragma warning disable CS1591
public interface FontEffect
{
    ///<summary> Whether to overlay the effect over the original texture. </summary>
    bool Overlay { get; }

    ///<summary> The vector radius of the font effect. </summary>
    SizeF Measure { get; }

    ///<summary> Draws the font effect over the texture. </summary>
    void Draw(Bitmap bitmap, Graphics textGraphics, GraphicsPath path, float x, float y);
}

/// <summary> Stores information about a font's appearance. </summary>
/// <remarks> Creates a new <see cref="FontDescription" /> storing a descriptor for <see cref="FontGenerator" />. </remarks>
/// <param name="fontPath"> The path to the font file. </param>
/// <param name="fontSize"> The relative size of the font. </param>
/// <param name="color"> The coloring tint of the font. </param>
/// <param name="padding"> Allocate extra space around the font when generating it. </param>
/// <param name="fontStyle"> Format/style of the font. </param>
/// <param name="trimTransparency"> Trim transparent space around the font. </param>
/// <param name="effectsOnly"> Leave out the original font and keep the effects. </param>
/// <param name="debug"> Draw a randomly colored background behind the font. </param>
public class FontDescription(
    string fontPath, int fontSize = 76, FontColor color = default, Vector2 padding = default,
    FontStyle fontStyle = default, bool trimTransparency = true, bool effectsOnly = false, bool debug = false)
{
    ///<summary> The path to the font file. </summary>
    public string FontPath => fontPath;

    ///<summary> The relative size of the font. </summary>
    public int FontSize => fontSize;

    ///<summary> The coloring tint of the font. </summary>
    public FontColor Color => color;

    ///<summary> How much extra space is allocated around the font when generating it. </summary>
    public Vector2 Padding => padding;

    ///<summary> The format/style of the font (for example: bold, italics, etc). </summary>
    public FontStyle FontStyle => fontStyle;

    /// <summary> Trim transparent space around the font. Should always be <see langword="true" />. </summary>
    public bool TrimTransparency => trimTransparency;

    ///<summary> Leave out the original font and keep the effects. </summary>
    public bool EffectsOnly => effectsOnly;

    ///<summary> Draw a randomly colored background behind the font. </summary>
    public bool Debug => debug;
}