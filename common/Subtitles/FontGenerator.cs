namespace StorybrewCommon.Subtitles;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using BrewLib.Util;
using Scripting;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Storyboarding;
using Path = System.IO.Path;

/// <summary> Stores information about a font image. </summary>
/// <remarks> Creates a new <see cref="FontTexture"/> storing information of the texture. </remarks>
/// <param name="path"> The path to the font texture. </param>
/// <param name="offsetX"> The texture offset in X-units. </param>
/// <param name="offsetY"> The texture offset in Y-units. </param>
/// <param name="baseWidth"> The base width of the texture in pixels. </param>
/// <param name="baseHeight"> The base height of the texture in pixels. </param>
/// <param name="width"> The actual width of the texture in pixels. </param>
/// <param name="height"> The actual width of the texture in pixels. </param>
/// <param name="paths"> The path data which comprises this font. </param>
public class FontTexture(string path,
    float offsetX,
    float offsetY,
    int baseWidth,
    int baseHeight,
    int width,
    int height,
    IPathCollection paths)
{
    ///<summary> The path to the font texture. </summary>
    public string Path => path;

    /// <returns> <see langword="true"/> if the texture does not exist. </returns>
    public bool IsEmpty => path is null;

    ///<summary> The texture offset in X-units. </summary>
    public float OffsetX => offsetX;

    ///<summary> The texture offset in Y-units. </summary>
    public float OffsetY => offsetY;

    ///<summary> The original width of the texture in pixels. </summary>
    public int BaseWidth => baseWidth;

    ///<summary> The original height of the texture in pixels. </summary>
    public int BaseHeight => baseHeight;

    ///<summary> The actual width of the texture in pixels. </summary>
    public int Width => width;

    ///<summary> The actual width of the texture in pixels. </summary>
    public int Height => height;

    ///<summary> The line segments that comprise this text. </summary>
    public IPathCollection PathData => paths;

    /// <summary> Gets the font offset for the given <see cref="OsbOrigin"/>. </summary>
    public Vector2 OffsetFor(OsbOrigin origin) => origin switch
    {
        OsbOrigin.TopCentre => new(OffsetX + Width * .5f, OffsetY),
        OsbOrigin.TopRight => new(OffsetX + Width, OffsetY),
        OsbOrigin.CentreLeft => new(OffsetX, OffsetY + Height * .5f),
        OsbOrigin.Centre => new(OffsetX + Width * .5f, OffsetY + Height * .5f),
        OsbOrigin.CentreRight => new(OffsetX + Width, OffsetY + Height * .5f),
        OsbOrigin.BottomLeft => new(OffsetX, OffsetY + Height),
        OsbOrigin.BottomCentre => new(OffsetX + Width * .5f, OffsetY + Height),
        OsbOrigin.BottomRight => new(OffsetX + Width, OffsetY + Height),
        _ => new(OffsetX, OffsetY)
    };
}

///<summary> A class that generates and manages font textures. </summary>
public sealed class FontGenerator
{
    internal static readonly DrawingOptions options = new()
    {
        ShapeOptions = new() { IntersectionRule = IntersectionRule.NonZero }
    };

    readonly string assetDirectory;

    readonly Dictionary<string, FontTexture> cache = [];
    readonly FontDescription description;
    readonly FontEffect[] effects;

    readonly float emSize;

    readonly TextOptions format;
    readonly SolidBrush textBrush;

    internal FontGenerator(string dir, FontDescription desc, FontEffect[] fx, string projDir, string assetDir)
    {
        Directory = dir;
        description = desc;
        effects = fx;
        assetDirectory = assetDir;

        textBrush = new(description.Color);

        var fontPath = Path.Combine(projDir, description.FontPath);
        if (!File.Exists(fontPath)) fontPath = description.FontPath;

        FontFamily family = default;
        if (File.Exists(fontPath))
        {
            FontCollection collection = new();
            family = collection.Add(fontPath, CultureInfo.InvariantCulture);
        }

        const float dpi = 72f;
        emSize = 96f * description.FontSize / dpi;
        var font = family == default ?
            SystemFonts.CreateFont(fontPath, emSize, description.FontStyle) :
            new(family, emSize, description.FontStyle);

        var foundMetrics = font.Family.TryGetMetrics(desc.FontStyle, out var metrics);
        format = new(font)
        {
            TextAlignment = TextAlignment.Center,
            HintingMode = HintingMode.Standard,
            KerningMode = KerningMode.Standard,
            LineSpacing = foundMetrics ? metrics.VerticalMetrics.LineHeight * .001f : 1,
            Dpi = dpi
        };
    }

    /// <summary> The directory to the font textures. </summary>
    public string Directory { get; }

    ///<summary> Gets the texture path of the matching item's string representation. </summary>
    public FontTexture GetTexture(object obj)
    {
        var text = Convert.ToString(obj, CultureInfo.InvariantCulture);
        if (!cache.TryGetValue(text, out var texture)) cache[text] = texture = generateTexture(text);
        return texture;
    }

    FontTexture generateTexture(string text)
    {
        var measuredSize = TextMeasurer.MeasureAdvance(text, format);
        var baseWidth = (int)float.Ceiling(measuredSize.Width);
        var baseHeight = (int)float.Ceiling(measuredSize.Height);

        float effectsWidth = 0, effectsHeight = 0;
        foreach (var t in effects)
        {
            var effectSize = t.Measure;
            effectsWidth = Math.Max(effectsWidth, effectSize.Width);
            effectsHeight = Math.Max(effectsHeight, effectSize.Height);
        }

        var padding = description.Padding;

        var width = (int)float.Ceiling(baseWidth + effectsWidth + padding.X * 2);
        var height = (int)float.Ceiling(baseHeight + effectsHeight + padding.Y * 2);

        var paddingX = padding.X + effectsWidth / 2;
        var paddingY = padding.Y + effectsHeight / 2;
        var x = paddingX + measuredSize.Width / 2;

        var offsetX = -paddingX;
        var offsetY = -paddingY;

        if (string.IsNullOrWhiteSpace(text) || width == 0 || height == 0)
            return new(null, offsetX, offsetY, baseWidth, baseHeight, width, height, null);

        var segments = TextBuilder.GenerateGlyphs(text, format).Translate(paddingX, paddingY);

        Image<Rgba32> realText = new(width, height);
        realText.Mutate(b =>
        {
            if (description.Debug)
            {
                FastRandom r = new(cache.Count);
                b.Clear(new Rgb24((byte)r.Next(100, 255), (byte)r.Next(100, 255), (byte)r.Next(100, 255)));
            }

            foreach (var t in effects)
                if (!t.Overlay)
                    t.Draw(b, segments, x, paddingY);

            if (!description.EffectsOnly) b.Fill(options, textBrush, segments);
            foreach (var t in effects)
                if (t.Overlay)
                    t.Draw(b, segments, x, paddingY);

            if (!description.Debug) return;

            b.DrawLine(Color.Red, 1, new(x, paddingY), new(x, paddingY + baseHeight)).DrawLine(Color.Red, 1,
                new(x - baseWidth * .5f, paddingY), new(x + baseWidth * .5f, paddingY));
        });

        var bounds = description.TrimTransparency ? BitmapHelper.FindTransparencyBounds(realText) : default;
        var validBounds = !bounds.IsEmpty && !bounds.Equals(new(0, 0, width, height));
        if (validBounds)
        {
            offsetX += bounds.X;
            offsetY += bounds.Y;
            width = bounds.Width;
            height = bounds.Height;
        }

        string filename = null;
        var trimExist = false;
        var trimmedText = description.TrimTransparency ? text.Trim() : null;

        if (description.TrimTransparency)
        {
            var foundTrim = cache.Keys.FirstOrDefault(s => s.AsSpan().Trim().Equals(trimmedText, StringComparison.Ordinal));
            if (foundTrim is not null)
            {
                trimExist = true;
                filename = Path.GetFileName(cache[foundTrim].Path);
            }
        }

        filename ??= (trimmedText ?? text).Length == 1 ?
            $"{(!PathHelper.IsValidFilename(char.ToString(text[0])) ?
                ((int)text[0]).ToString("x4", CultureInfo.InvariantCulture).TrimStart('0') :
                char.IsUpper(text[0]) ? char.ToLower(text[0], CultureInfo.InvariantCulture).ToString() + '_' :
                    char.ToString(text[0]))}.png" :
            $"_{cache.Count(l => l.Key.AsSpan().Trim().Length > 1)
                .ToString("x3", CultureInfo.InvariantCulture).TrimStart('0')}.png";

        var texturePath = Path.Combine(Directory, filename);
        PathHelper.WithStandardSeparatorsUnsafe(texturePath);

        if (trimExist)
        {
            realText.Dispose();
            return new(texturePath, offsetX, offsetY, baseWidth, baseHeight, width, height, segments);
        }

        var path = Path.Combine(assetDirectory, texturePath);
        using (var stream = File.Create(path))
        {
            if (validBounds) realText.Mutate(b => b.Crop(bounds));
            realText.SaveAsPng(stream);
        }

        StoryboardObjectGenerator.Current.bitmaps[path] = realText;
        if (path.Contains(StoryboardObjectGenerator.Current.MapsetPath) ||
            path.Contains(StoryboardObjectGenerator.Current.AssetPath))
            StoryboardObjectGenerator.Current.Compressor.Compress(path, new(0, 75, 1));

        return new(texturePath, offsetX, offsetY, baseWidth, baseHeight, width, height, segments);
    }
}