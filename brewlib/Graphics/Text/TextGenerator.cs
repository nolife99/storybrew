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
using Util;

public sealed class TextGenerator(ResourceContainer resourceContainer)
{
    readonly Dictionary<string, FontCollection> fontCollections = [];
    readonly Dictionary<string, FontFamily> fontFamilies = [];

    readonly Dictionary<int, Font> fonts = [];
    readonly SolidBrush fill = new(Color.White), shadow = new(new Rgba32(0, 0, 0, 220));

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
            HorizontalAlignment = (alignment & BoxAlignment.Horizontal) switch
            {
                BoxAlignment.Left => HorizontalAlignment.Left,
                BoxAlignment.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center
            },
            VerticalAlignment = (alignment & BoxAlignment.Vertical) switch
            {
                BoxAlignment.Top => VerticalAlignment.Top,
                BoxAlignment.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Center
            },
            HintingMode = HintingMode.Standard,
            KerningMode = KerningMode.Standard
        };
        var measuredSize = TextMeasurer.MeasureAdvance(text, options);

        var width = (int)(measuredSize.Width + padding.X * 2 + 1);
        var height = (int)(measuredSize.Height + padding.Y * 2 + 1);

        textureSize = new(width, height);
        if (measureOnly) return null;

        Image<Rgba32> bitmap = new(width, height);
        bitmap.Mutate(b =>
            b.DrawText(text, font, shadow, new PointF(padding.X + 1, padding.Y + 1))
                .DrawText(text, font, fill, new PointF(padding.X, padding.Y)));

        return bitmap;
    }

    Font getFont(string name, float emSize, FontStyle style)
    {
        var identifier = HashCode.Combine(name, emSize, style);
        if (fonts.TryGetValue(identifier, out var font)) return font;

        if (!fontFamilies.TryGetValue(name, out var fontFamily))
        {
            using (var stream = resourceContainer.GetStream(name, ResourceSource.Embedded))
                if (stream is not null)
                {
                    if (!fontCollections.TryGetValue(name, out var collection)) fontCollections[name] = collection = new();
                    Trace.WriteLine($"Loaded font {(fontFamily = collection.Add(stream, CultureInfo.InvariantCulture)).Name} for {name}");
                }

            fontFamilies[name] = fontFamily;
        }

        if (fontFamily != default) font = new(fontFamily, emSize, style);
        else
        {
            font = SystemFonts.CreateFont(name, CultureInfo.InvariantCulture, emSize, style);
            Trace.WriteLine($"Using system font for {name}");
        }

        fonts[identifier] = font;
        return font;
    }
}