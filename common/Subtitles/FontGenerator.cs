using BrewLib.Util;
using StorybrewCommon.Scripting;
using StorybrewCommon.Storyboarding;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Numerics;
using System.IO;
using System.Linq;
using System.Globalization;

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
public class FontGenerator : IDisposable
{
    /// <summary> The directory to the font textures. </summary>
    public string Directory { get; }

    internal Dictionary<string, FontTexture> cache = [];
    readonly string projectDirectory, assetDirectory;

    readonly FontDescription description;
    readonly FontEffect[] effects;
    readonly float emSize;

    StringFormat format;
    Graphics graphics;
    PrivateFontCollection collection;
    Font font;
    FontFamily family;

    public FontGenerator(string dir, FontDescription desc, FontEffect[] fx, string projDir, string assetDir)
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

        graphics = Graphics.FromHwnd(0);
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.InterpolationMode = InterpolationMode.Bilinear;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.PixelOffsetMode = PixelOffsetMode.None;

        collection = new();

        var fontPath = Path.Combine(projectDirectory, description.FontPath);
        if (!File.Exists(fontPath)) fontPath = description.FontPath;
        if (File.Exists(fontPath)) collection.AddFontFile(fontPath);

        family = File.Exists(fontPath) ? collection.Families[0] : null;

        var dpiScale = 96 / graphics.DpiY;
        font = family is null ? new(fontPath, description.FontSize * dpiScale, description.FontStyle) : new(family, description.FontSize * dpiScale, description.FontStyle);
        emSize = graphics.DpiY * font.SizeInPoints / 72;
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

        float offsetX = 0, offsetY = 0;
        int baseWidth, baseHeight, width, height;

        var measuredSize = graphics.MeasureString(text, font, 0, format);
        baseWidth = (int)MathF.Ceiling(measuredSize.Width);
        baseHeight = (int)MathF.Ceiling(measuredSize.Height);

        float effectsWidth = 0, effectsHeight = 0;
        for (var i = 0; i < effects.Length; ++i)
        {
            var effectSize = effects[i].Measure;
            effectsWidth = Math.Max(effectsWidth, effectSize.Width);
            effectsHeight = Math.Max(effectsHeight, effectSize.Height);
        }
        width = (int)MathF.Ceiling(baseWidth + effectsWidth + description.Padding.X * 2);
        height = (int)MathF.Ceiling(baseHeight + effectsHeight + description.Padding.Y * 2);

        var paddingX = description.Padding.X + effectsWidth / 2;
        var paddingY = description.Padding.Y + effectsHeight / 2;
        var x = paddingX + measuredSize.Width / 2;
        var y = paddingY;

        offsetX = -paddingX;
        offsetY = -paddingY;

        if (string.IsNullOrWhiteSpace(text) || width == 0 || height == 0) return new(null, offsetX, offsetY, baseWidth, baseHeight, width, height, null);

        Bitmap realText = new(width, height);
        using GraphicsPath segments = new(FillMode.Winding);

        using (var textGraphics = Graphics.FromImage(realText))
        {
            textGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            textGraphics.SmoothingMode = SmoothingMode.HighQuality;
            textGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            textGraphics.CompositingQuality = CompositingQuality.HighQuality;
            textGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (description.Debug)
            {
                FastRandom r = new(cache.Count);
                textGraphics.Clear(Color.FromArgb(r.Next(100, 255), r.Next(100, 255), r.Next(100, 255)));
            }

            segments.AddString(text, font.FontFamily, (int)description.FontStyle, emSize, new PointF(x, y), format);

            for (var i = 0; i < effects.Length; ++i) if (!effects[i].Overlay) effects[i].Draw(realText, textGraphics, segments, x, y);
            if (!description.EffectsOnly) using (SolidBrush brush = new(description.Color)) textGraphics.FillPath(brush, segments);
            for (var i = 0; i < effects.Length; ++i) if (effects[i].Overlay) effects[i].Draw(realText, textGraphics, segments, x, y);

            if (description.Debug) using (Pen pen = new(Color.Red))
            {
                textGraphics.DrawLine(pen, x, y, x, y + baseHeight);
                textGraphics.DrawLine(pen, x - baseWidth * .5f, y, x + baseWidth * .5f, y);
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

        filename ??= (description.TrimTransparency ? text.Trim() : text).Length == 1 ?
            $"{(!PathHelper.IsValidFilename(char.ToString(text[0])) ? ((int)text[0]).ToString("x4", CultureInfo.InvariantCulture).TrimStart('0') : 
                (char.IsUpper(text[0]) ? char.ToLower(text[0], CultureInfo.InvariantCulture) + '_' : char.ToString(text[0])))}.png" :
            $"_{cache.Count(l => l.Key.Trim().Length > 1).ToString("x3", CultureInfo.InvariantCulture).TrimStart('0')}.png";

        var path = Path.Combine(assetDirectory, Directory, filename);
        if (!trimExist)
        {
            using (FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                if (validBounds) using (var trim = realText.FastCloneSection(bounds)) Misc.WithRetries(() => trim.Save(stream, ImageFormat.Png));
                else Misc.WithRetries(() => realText.Save(stream, ImageFormat.Png));
            }

            StoryboardObjectGenerator.Current.bitmaps[path] = realText;
            StoryboardObjectGenerator.Current.Compressor.LosslessCompress(path, new(
                path.Contains(StoryboardObjectGenerator.Current.MapsetPath) || path.Contains(StoryboardObjectGenerator.Current.AssetPath) ? 7 : 2));
        }
        return new(PathHelper.WithStandardSeparators(Path.Combine(Directory, filename)), offsetX, offsetY, baseWidth, baseHeight, width, height, segments.PathData);
    }

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            format.Dispose();
            graphics.Dispose();
            collection.Dispose();
            font.Dispose();
            family?.Dispose();

            format = null;
            graphics = null;
            collection = null;
            font = null;
            family = null;

            disposed = true;
        }
    }
}