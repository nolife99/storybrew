namespace StorybrewCommon.Subtitles;

using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Processing;

#pragma warning disable CS1591
public interface FontEffect
{
    ///<summary> Overlays the effect over the original texture. </summary>
    bool Overlay { get; }

    ///<summary> The radius of the font effect. </summary>
    SizeF Measure { get; }

    ///<summary> Draws the font effect over the texture. </summary>
    void Draw(IImageProcessingContext bitmap, IPathCollection path, float x, float y);
}

/// <summary> Describes a font's appearance. </summary>
public record FontDescription
{
    /// <summary> Creates a new <see cref="FontDescription"/>. </summary>
    /// <param name="fontPath"> The path to the font file. </param>
    /// <param name="fontSize"> The relative size of the font. </param>
    /// <param name="color"> The coloring tint of the font. </param>
    /// <param name="padding"> Allocate extra space around the font when generating it. </param>
    /// <param name="fontStyle"> The format/style of the font. </param>
    /// <param name="trimTransparency"> Crop excess transparent space from the font. </param>
    /// <param name="effectsOnly"> Leave out the original font and keep the effects. </param>
    /// <param name="debug"> Draw a randomly colored background behind the font. </param>
    public FontDescription(string fontPath,
        int fontSize = 76,
        Color color = default,
        Vector2 padding = default,
        FontStyle fontStyle = default,
        bool trimTransparency = true,
        bool effectsOnly = false,
        bool debug = false)
    {
        FontPath = fontPath;
        FontSize = fontSize;
        Color = color;
        Padding = padding;
        FontStyle = fontStyle;
        TrimTransparency = trimTransparency;
        EffectsOnly = effectsOnly;
        Debug = debug;
    }

    /// <summary> The path to the font file. </summary>
    public string FontPath { get; init; }

    /// <summary> The relative size of the font. </summary>
    public int FontSize { get; init; }

    /// <summary> The coloring tint of the font. </summary>
    public Color Color { get; init; }

    /// <summary> Allocate extra space around the font when generating it. </summary>
    public Vector2 Padding { get; init; }

    /// <summary> Format/style of the font. </summary>
    public FontStyle FontStyle { get; init; }

    /// <summary> Trim transparent space around the font. </summary>
    public bool TrimTransparency { get; init; }

    /// <summary> Leave out the original font and keep the effects. </summary>
    public bool EffectsOnly { get; init; }

    /// <summary> Draw a randomly colored background behind the font. </summary>
    public bool Debug { get; init; }
}