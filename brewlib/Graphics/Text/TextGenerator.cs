namespace BrewLib.Graphics.Text;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Data;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Textures;
using Util;

public sealed class TextGenerator(ResourceContainer resourceContainer)
{
    readonly DrawingOptions drawOptions = new() { GraphicsOptions = new() { AntialiasSubpixelDepth = 2 } };
    readonly FontFamily[] fallback = [SystemFonts.Get("Segoe UI Symbol", CultureInfo.InvariantCulture)];
    readonly SolidBrush fill = new(Color.White), shadow = new(Color.FromRgba(0, 0, 0, 220));

    readonly FontCollection fontCollection = new();
    readonly Dictionary<int, Font> fonts = [];

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
            LineSpacing = Math.Clamp(font.FontMetrics.VerticalMetrics.LineHeight * .001f, 1, 1.5f),
            FallbackFontFamilies = fallback
        };

        var measuredSize = TextMeasurer.MeasureAdvance(text, options);
        var width = (int)(measuredSize.Width + padding.X * 2 + 1);
        var height = (int)(measuredSize.Height + padding.Y * 2 + 1);

        textureSize = new(width, height);
        if (measureOnly) return null;

        Image<Rgba32> bitmap = new(Texture2d.ContiguousBufferDecoderOptions.Configuration, width, height);
        bitmap.Mutate(b =>
        {
            RichTextOptions textOptions = new(font) { Origin = padding, FallbackFontFamilies = fallback };
            b.DrawText(drawOptions, new(textOptions) { Origin = padding + Vector2.One }, text, shadow, null)
                .DrawText(drawOptions, textOptions, text, fill, null);
        });

        return bitmap;
    }

    Font getFont(string name, float emSize, FontStyle style)
    {
        var identifier = HashCode.Combine(name, emSize, style);
        if (fonts.TryGetValue(identifier, out var font)) return font;

        if (!fontCollection.TryGet(name, out var fontFamily))
        {
            using var stream = resourceContainer.GetStream(name, ResourceSource.Embedded);
            if (stream is not null)
                Trace.WriteLine(
                    $"Loaded font {(fontFamily = fontCollection.Add(stream, CultureInfo.InvariantCulture)).Name} for {name}");
        }

        if (fontFamily != default) font = new(fontFamily, emSize, style);
        else
        {
            font = SystemFonts.CreateFont(name, emSize, style);
            Trace.WriteLine($"Using system font for {name}");
        }

        fonts[identifier] = font;
        return font;
    }
}