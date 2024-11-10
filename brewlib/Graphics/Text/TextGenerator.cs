namespace BrewLib.Graphics.Text;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;
using CommunityToolkit.HighPerformance.Buffers;
using Data;
using Util;

public sealed class TextGenerator : IDisposable
{
    ResourceContainer container;
    Dictionary<string, PrivateFontCollection> fontCollections = [];
    Dictionary<string, FontFamily> fontFamilies = [];

    Dictionary<int, Font> fonts = [];
    Graphics metrics;
    SolidBrush shadow = new(Color.FromArgb(220, 0, 0, 0));

    public TextGenerator(ResourceContainer resourceContainer)
    {
        metrics = Graphics.FromHwnd(0);
        metrics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
        metrics.InterpolationMode = InterpolationMode.NearestNeighbor;

        container = resourceContainer;
    }

    public Bitmap CreateBitmap(string text,
        string fontName,
        float fontSize,
        SizeF maxSize,
        Vector2 padding,
        BoxAlignment alignment,
        StringTrimming trimming,
        out Vector2 textureSize,
        bool measureOnly)
    {
        if (string.IsNullOrEmpty(text)) text = " ";
        using StringFormat stringFormat = new(StringFormat.GenericTypographic)
        {
            Alignment =
                (alignment & BoxAlignment.Horizontal) switch
                {
                    BoxAlignment.Left => StringAlignment.Near,
                    BoxAlignment.Right => StringAlignment.Far,
                    _ => StringAlignment.Center
                },
            LineAlignment =
                (alignment & BoxAlignment.Vertical) switch
                {
                    BoxAlignment.Top => StringAlignment.Near,
                    BoxAlignment.Bottom => StringAlignment.Far,
                    _ => StringAlignment.Center
                },
            Trimming = trimming,
            FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip
        };

        var font = getFont(fontName, 96 / metrics.DpiY * fontSize, FontStyle.Regular);
        var measuredSize = metrics.MeasureString(text, font, maxSize, stringFormat);

        var width = (int)(measuredSize.Width + padding.X * 2 + 1);
        var height = (int)(measuredSize.Height + padding.Y * 2 + 1);

        textureSize = new(width, height);
        if (measureOnly) return null;

        Bitmap bitmap = new(width, height);
        using var textGraphics = Graphics.FromImage(bitmap);

        textGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        textGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        textGraphics.PixelOffsetMode = PixelOffsetMode.Half;

        textGraphics.DrawString(text, font, shadow, new RectangleF(padding.X + 1, padding.Y + 1, width, height), stringFormat);
        textGraphics.DrawString(text, font, Brushes.White, new RectangleF(padding.X, padding.Y, width, height), stringFormat);

        return bitmap;
    }

    unsafe Font getFont(string name, float emSize, FontStyle style)
    {
        var identifier = HashCode.Combine(name, emSize, style);
        if (fonts.TryGetValue(identifier, out var font)) return font;

        if (!fontFamilies.TryGetValue(name, out var fontFamily))
        {
            using (var stream = container.GetStream(name, ResourceSource.Embedded))
                if (stream is not null)
                {
                    if (!fontCollections.TryGetValue(name, out var collection)) fontCollections[name] = collection = new();

                    var len = (int)stream.Length;
                    using (var span = SpanOwner<byte>.Allocate(len))
                    {
                        var arr = span.Span;
                        var read = stream.Read(arr);
                        fixed (void* pinned = arr) collection.AddMemoryFont((nint)pinned, read);
                    }

                    var families = collection.Families;
                    if (families.Length == 1) Trace.WriteLine($"Loaded font {(fontFamily = families[0]).Name} for {name}");
                    else
                    {
                        Trace.TraceError($"Failed to load font {name}: Expected one family, got {families.Length}");
                        foreach (var family in families) family.Dispose();
                    }
                }

            fontFamilies[name] = fontFamily;
        }

        if (fontFamily is not null) font = new(fontFamily, emSize, style);
        else
        {
            font = new(name, emSize, style);
            Trace.WriteLine($"Using font system font for {name}");
        }

        fonts[identifier] = font;
        return font;
    }

    #region IDisposable Support

    bool disposed;
    void Dispose(bool disposing)
    {
        if (disposed) return;

        metrics.Dispose();
        shadow.Dispose();
        fonts.Dispose();
        fontFamilies.Dispose();
        fontCollections.Dispose();

        if (disposing)
        {
            container = null;
            metrics = null;
            shadow = null;
            fonts = null;
            fontCollections = null;
            fontFamilies = null;
        }

        disposed = true;
    }

    ~TextGenerator() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}