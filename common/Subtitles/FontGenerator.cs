namespace StorybrewCommon.Subtitles;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using BrewLib.Util;
using Scripting;
using Storyboarding;

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
    PathData paths)
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
    public PathData PathData => paths;

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
public sealed class FontGenerator : IDisposable
{
    readonly string assetDirectory;

    readonly Dictionary<string, FontTexture> cache = [];
    readonly PrivateFontCollection collection;
    readonly FontDescription description;
    readonly FontEffect[] effects;

    readonly float emSize;
    readonly Font font;

    readonly StringFormat format;
    readonly Graphics metrics;
    readonly SolidBrush textBrush;

    bool disposed;

    internal FontGenerator(string dir, FontDescription desc, FontEffect[] fx, string projDir, string assetDir)
    {
        Directory = dir;
        description = desc;
        effects = fx;
        assetDirectory = assetDir;

        format = new(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip
        };

        textBrush = new(description.Color);

        metrics = Graphics.FromHwnd(0);
        metrics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
        metrics.InterpolationMode = InterpolationMode.NearestNeighbor;

        var fontPath = Path.Combine(projDir, description.FontPath);
        if (!File.Exists(fontPath)) fontPath = description.FontPath;

        FontFamily[] family = null;
        if (File.Exists(fontPath))
        {
            collection = new();
            collection.AddFontFile(fontPath);
            family = collection.Families;
        }

        var ptSize = 96 / metrics.DpiY * description.FontSize;
        font = family is null ? new(fontPath, ptSize, description.FontStyle) : new(family[0], ptSize, description.FontStyle);
        if (family is not null && family.Length > 1)
            for (var i = 1; i < family.Length; ++i)
                family[i].Dispose();

        emSize = font.GetHeight(metrics) * font.FontFamily.GetEmHeight(description.FontStyle) /
            font.FontFamily.GetLineSpacing(description.FontStyle);
    }

    /// <summary> The directory to the font textures. </summary>
    public string Directory { get; }

    /// <summary> Deletes and frees all loaded fonts from memory. </summary>
    /// <remarks> Do not call from script code. This is automatically disposed at the end of script execution. </remarks>
    public void Dispose()
    {
        if (disposed) return;

        format.Dispose();
        metrics.Dispose();
        textBrush.Dispose();
        font.Dispose();
        collection?.Dispose();

        disposed = true;
    }

    ///<summary> Gets the texture path of the matching item's string representation. </summary>
    public FontTexture GetTexture(object obj)
    {
        var text = Convert.ToString(obj, CultureInfo.InvariantCulture);
        if (!cache.TryGetValue(text, out var texture)) cache[text] = texture = generateTexture(text);
        return texture;
    }

    FontTexture generateTexture(string text)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var measuredSize = metrics.MeasureString(text, font, 0, format);
        var baseWidth = (int)float.Ceiling(measuredSize.Width);
        var baseHeight = (int)float.Ceiling(measuredSize.Height);

        float effectsWidth = 0, effectsHeight = 0;
        foreach (var t in effects)
        {
            var effectSize = t.Measure;
            effectsWidth = Math.Max(effectsWidth, effectSize.Width);
            effectsHeight = Math.Max(effectsHeight, effectSize.Height);
        }

        var width = (int)float.Ceiling(baseWidth + effectsWidth + description.Padding.X * 2);
        var height = (int)float.Ceiling(baseHeight + effectsHeight + description.Padding.Y * 2);

        var paddingX = description.Padding.X + effectsWidth / 2;
        var paddingY = description.Padding.Y + effectsHeight / 2;
        var x = paddingX + measuredSize.Width / 2;

        var offsetX = -paddingX;
        var offsetY = -paddingY;

        if (string.IsNullOrWhiteSpace(text) || width == 0 || height == 0)
            return new(null, offsetX, offsetY, baseWidth, baseHeight, width, height, null);

        Bitmap realText = new(width, height);
        using GraphicsPath segments = new(FillMode.Winding);

        using (var textGraphics = Graphics.FromImage(realText))
        {
            textGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            textGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            textGraphics.CompositingQuality = CompositingQuality.HighQuality;
            textGraphics.PixelOffsetMode = PixelOffsetMode.Half;

            if (description.Debug)
            {
                FastRandom r = new(cache.Count);
                textGraphics.Clear(Color.FromArgb(r.Next(100, 255), r.Next(100, 255), r.Next(100, 255)));
            }

            segments.AddString(text, font.FontFamily, (int)description.FontStyle, emSize, new PointF(x, paddingY), format);

            foreach (var t in effects)
                if (!t.Overlay)
                    t.Draw(realText, textGraphics, segments, x, paddingY);

            if (!description.EffectsOnly) textGraphics.FillPath(textBrush, segments);
            foreach (var t in effects)
                if (t.Overlay)
                    t.Draw(realText, textGraphics, segments, x, paddingY);

            if (description.Debug)
            {
                textGraphics.DrawLine(Pens.Red, x, paddingY, x, paddingY + baseHeight);
                textGraphics.DrawLine(Pens.Red, x - baseWidth * .5f, paddingY, x + baseWidth * .5f, paddingY);
            }
        }

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
            return new(texturePath, offsetX, offsetY, baseWidth, baseHeight, width, height, segments.PathData);
        }

        var path = Path.Combine(assetDirectory, texturePath);
        using (var stream = File.Create(path))
            if (validBounds)
            {
                using var cropped = realText.FastCloneSection(bounds);
                realText.Dispose();
                realText = cropped;
                Misc.WithRetries(() => cropped.Save(stream, ImageFormat.Png));
            }
            else Misc.WithRetries(() => realText.Save(stream, ImageFormat.Png));

        StoryboardObjectGenerator.Current.bitmaps[path] = realText;
        if (path.Contains(StoryboardObjectGenerator.Current.MapsetPath) ||
            path.Contains(StoryboardObjectGenerator.Current.AssetPath))
            StoryboardObjectGenerator.Current.Compressor.Compress(path, new(0, 75, 1));

        return new(texturePath, offsetX, offsetY, baseWidth, baseHeight, width, height, segments.PathData);
    }
}