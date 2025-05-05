namespace BrewLib.Graphics.Text;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Collections.Pooled;
using IO;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Util;

public sealed class TextGenerator(ResourceContainer resourceContainer) : IDisposable
{
    static readonly DrawingOptions drawOptions = new() { GraphicsOptions = new() { AntialiasSubpixelDepth = 2 } };
    static readonly SolidBrush fill = new(Color.White), shadow = new(Color.FromPixel(new Rgba32(0, 0, 0, 220)));
    readonly PooledDictionary<string, FontFamily> families = new();

    readonly FontCollection fontCollection = new();
    readonly PooledDictionary<int, Font> fonts = new();
    IReadOnlyList<FontFamily> fallback;

    public void Dispose()
    {
        families.Dispose();
        fonts.Dispose();
    }

    public Image<Rgba32> CreateBitmap(string text,
        string fontName,
        float fontSize,
        Vector2 padding,
        BoxAlignment alignment,
        out Vector2 textureSize,
        bool measureOnly)
    {
        if (string.IsNullOrEmpty(text)) text = " ";

        var font = getFont(fontName, 96 * fontSize / 72, FontStyle.Regular);
        TextOptions options = new(font)
        {
            HorizontalAlignment =
                (alignment & BoxAlignment.Horizontal) switch
                {
                    BoxAlignment.Left => HorizontalAlignment.Left,
                    BoxAlignment.Right => HorizontalAlignment.Right,
                    _ => HorizontalAlignment.Center
                },
            VerticalAlignment =
                (alignment & BoxAlignment.Vertical) switch
                {
                    BoxAlignment.Top => VerticalAlignment.Top,
                    BoxAlignment.Bottom => VerticalAlignment.Bottom,
                    _ => VerticalAlignment.Center
                },
            LineSpacing = float.Clamp(font.FontMetrics.VerticalMetrics.LineHeight * .001f, 1, 1.5f),
            FallbackFontFamilies = fallback ??= [getFamily("MaterialIcons.ttf")]
        };

        var measuredSize = TextMeasurer.MeasureAdvance(text, options);
        var width = (int)(measuredSize.Width + padding.X * 2 + 1);
        var height = (int)(measuredSize.Height + padding.Y * 2 + 1);

        textureSize = new(width, height);
        if (measureOnly) return null;

        Image<Rgba32> bitmap = new(width, height);
        RichTextOptions textOptions = new(font) { Origin = padding, FallbackFontFamilies = fallback },
            shadowTextOptions = new(textOptions) { Origin = padding + Vector2.One };

        bitmap.Mutate(b =>
        {
            b.DrawText(drawOptions, shadowTextOptions, text, shadow, null)
                .DrawText(drawOptions, textOptions, text, fill, null);
        });

        return bitmap;
    }

    Font getFont(string name, float emSize, FontStyle style)
    {
        var id = HashCode.Combine(name, emSize, style);
        if (fonts.TryGetValue(id, out var font)) return font;

        var fontFamily = getFamily(name);
        if (fontFamily != default) font = new(fontFamily, emSize, style);
        else
        {
            font = SystemFonts.CreateFont(name, emSize, style);
            Trace.WriteLine($"Using system font for {name}");
        }

        return fonts[id] = font;
    }

    FontFamily getFamily(string name)
    {
        if (families.TryGetValue(name, out var fontFamily)) return fontFamily;

        using var stream = resourceContainer.GetStream(name, ResourceSource.Embedded);
        if (stream is null) return SystemFonts.Get(name, CultureInfo.InvariantCulture);

        Trace.WriteLine(
            $"Loaded font {(fontFamily = fontCollection.Add(stream, CultureInfo.InvariantCulture)).Name} for {name}");

        return families[name] = fontFamily;
    }
}