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
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;

namespace StorybrewCommon.Subtitles;

///<summary> Stores information about a font image. </summary>
///<remarks> Creates a new <see cref="FontTexture"/> storing information of the texture. </remarks>
///<param name="path"> The path to the font texture. </param>
///<param name="offsetX"> The texture offset in X-units. </param>
///<param name="offsetY"> The texture offset in Y-units. </param>
///<param name="baseWidth"> The base width of the texture in pixels. </param>
///<param name="baseHeight"> The base height of the texture in pixels. </param>
///<param name="width"> The actual width of the texture in pixels. </param>
///<param name="height"> The actual width of the texture in pixels. </param>
///<param name="paths"> The path data which comprises this font. </param>
public class FontTexture(string path, float offsetX, float offsetY, int baseWidth, int baseHeight, int width, int height, PathData paths)
{
    ///<summary> The path to the font texture. </summary>
    public string Path => path;

    ///<returns> <see langword="true"/> if the texture does not exist. </returns>
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

    ///<summary> Gets the font offset for the given <see cref="OsbOrigin"/>. </summary>
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
        _ => new(OffsetX, OffsetY),
    };
}

///<summary> A class that generates and manages font textures. </summary>
public sealed class FontGenerator : IDisposable
{
    /// <summary> The directory to the font textures. </summary>
    public string Directory { get; }
    readonly string projectDirectory, assetDirectory;

    readonly float emSize;

    internal Dictionary<string, FontTexture> cache = [];
    FontDescription description;
    FontEffect[] effects;

    StringFormat format;
    Graphics metrics;
    Font font;
    PrivateFontCollection collection;
    SolidBrush textBrush;

    internal FontGenerator(string dir, FontDescription desc, FontEffect[] fx, string projDir, string assetDir)
    {
        Directory = dir;
        description = desc;
        effects = fx;
        projectDirectory = projDir;
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

        var fontPath = Path.Combine(projectDirectory, description.FontPath);
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
        if (family is not null && family.Length > 1) for (var i = 1; i < family.Length; ++i) family[i].Dispose();

        emSize = font.GetHeight(metrics) * font.FontFamily.GetEmHeight(description.FontStyle) / font.FontFamily.GetLineSpacing(description.FontStyle);
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
        var baseWidth = (int)MathF.Ceiling(measuredSize.Width);
        var baseHeight = (int)MathF.Ceiling(measuredSize.Height);

        float effectsWidth = 0, effectsHeight = 0;
        for (var i = 0; i < effects.Length; ++i)
        {
            var effectSize = effects[i].Measure;
            effectsWidth = Math.Max(effectsWidth, effectSize.Width);
            effectsHeight = Math.Max(effectsHeight, effectSize.Height);
        }
        var width = (int)MathF.Ceiling(baseWidth + effectsWidth + description.Padding.X * 2);
        var height = (int)MathF.Ceiling(baseHeight + effectsHeight + description.Padding.Y * 2);

        var paddingX = description.Padding.X + effectsWidth / 2;
        var paddingY = description.Padding.Y + effectsHeight / 2;
        var x = paddingX + measuredSize.Width / 2;

        var offsetX = -paddingX;
        var offsetY = -paddingY;

        if (string.IsNullOrWhiteSpace(text) || width == 0 || height == 0) return new(null, offsetX, offsetY, baseWidth, baseHeight, width, height, null);

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

            for (var i = 0; i < effects.Length; ++i) if (!effects[i].Overlay) effects[i].Draw(realText, textGraphics, segments, x, paddingY);
            if (!description.EffectsOnly) textGraphics.FillPath(textBrush, segments);
            for (var i = 0; i < effects.Length; ++i) if (effects[i].Overlay) effects[i].Draw(realText, textGraphics, segments, x, paddingY);

            if (description.Debug)
            {
                textGraphics.DrawLine(Pens.Red, x, paddingY, x, paddingY + baseHeight);
                textGraphics.DrawLine(Pens.Red, x - baseWidth * .5f, paddingY, x + baseWidth * .5f, paddingY);
            }
        }

        var bounds = description.TrimTransparency ? BitmapHelper.FindTransparencyBounds(realText) : default;
        var validBounds = !bounds.IsEmpty && !bounds.Equals(new(default, realText.Size));
        if (validBounds)
        {
            offsetX += bounds.X;
            offsetY += bounds.Y;
            width = bounds.Width;
            height = bounds.Height;
        }

        string filename = null;
        var trimExist = false;

        if (description.TrimTransparency && cache.TryGetValue(text.Trim(), out var texture))
        {
            trimExist = true;
            filename = Path.GetFileName(texture.Path);
        }

        filename ??= (description.TrimTransparency ? text.AsSpan().Trim() : text).Length == 1 ?
            $"{(!PathHelper.IsValidFilename(char.ToString(text[0])) ? ((int)text[0]).ToString("x4", CultureInfo.InvariantCulture).TrimStart('0') :
                (char.IsUpper(text[0]) ? char.ToLower(text[0], CultureInfo.InvariantCulture) + '_' : char.ToString(text[0])))}.png" :
            $"_{cache.Count(l => l.Key.AsSpan().Trim().Length > 1).ToString("x3", CultureInfo.InvariantCulture).TrimStart('0')}.png";

        var path = Path.Combine(assetDirectory, Directory, filename);
        if (!trimExist)
        {
            using (var stream = File.Create(path))
            {
                if (validBounds) using (var trim = realText.FastCloneSection(bounds))
                    {
                        realText.Dispose();
                        realText = trim;
                        Misc.WithRetries(() => trim.Save(stream, ImageFormat.Png));
                    }
                else Misc.WithRetries(() => realText.Save(stream, ImageFormat.Png));
            }

            StoryboardObjectGenerator.Current.bitmaps[path] = realText;
            if (path.Contains(StoryboardObjectGenerator.Current.MapsetPath) || path.Contains(StoryboardObjectGenerator.Current.AssetPath))
                StoryboardObjectGenerator.Current.Compressor.LosslessCompress(path, new(7));
        }
        return new(PathHelper.WithStandardSeparators(Path.Combine(Directory, filename)), offsetX, offsetY, baseWidth, baseHeight, width, height, segments.PathData);
    }

    bool disposed;

    ///<summary> Deletes and frees all loaded fonts from memory. </summary>
    ///<remarks> Do not call from script code. This is automatically disposed at the end of script execution. </remarks>
    public void Dispose() => Dispose(true);

    ///<summary/>
    ~FontGenerator() => Dispose(false);
    void Dispose(bool disposing)
    {
        if (!disposed)
        {
            format.Dispose();
            metrics.Dispose();
            textBrush.Dispose();
            font.Dispose();
            collection?.Dispose();

            if (disposing)
            {
                description = null;
                effects = null;
                format = null;
                metrics = null;
                textBrush = null;
                font = null;
                collection = null;

                disposed = true;
            }
        }
    }
}