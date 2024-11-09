namespace BrewLib.Graphics.Text;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;
using Data;
using Util;

public sealed class TextGenerator : IDisposable
{
    readonly LinkedList<string> recentlyUsedFonts = [];
    ResourceContainer container;
    Dictionary<string, PrivateFontCollection> fontCollections = [];
    Dictionary<string, FontFamily> fontFamilies = [];

    Dictionary<string, Font> fonts = [];
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
        var identifier = $"{name}.{emSize}.{(int)style}";

        if (fonts.TryGetValue(identifier, out var font))
        {
            recentlyUsedFonts.Remove(identifier);
            recentlyUsedFonts.AddFirst(identifier);
            return font;
        }

        recentlyUsedFonts.AddFirst(identifier);

        if (recentlyUsedFonts.Count > 64)
            while (recentlyUsedFonts.Count > 32)
            {
                var lastFontIdentifier = recentlyUsedFonts.Last.Value;
                recentlyUsedFonts.RemoveLast();

                fonts[lastFontIdentifier].Dispose();
                fonts.Remove(lastFontIdentifier);
            }

        if (!fontFamilies.TryGetValue(name, out var fontFamily))
        {
            using (var stream = container.GetStream(name, ResourceSource.Embedded))
                if (stream is not null)
                {
                    if (!fontCollections.TryGetValue(name, out var fontCollection))
                        fontCollections.Add(name, fontCollection = new());

                    var len = (int)stream.Length;
                    var arr = ArrayPool<byte>.Shared.Rent(len);
                    var read = stream.Read(arr, 0, len);

                    fixed (void* pinned = arr) fontCollection.AddMemoryFont((nint)pinned, read);
                    ArrayPool<byte>.Shared.Return(arr);

                    var families = fontCollection.Families;
                    if (families.Length == 1) Trace.WriteLine($"Loaded font {(fontFamily = families[0]).Name} for {name}");
                    else
                    {
                        Trace.TraceError($"Failed to load font {name}: Expected one family, got {families.Length}");
                        foreach (var family in families) family.Dispose();
                    }
                }

            fontFamilies.Add(name, fontFamily);
        }

        if (fontFamily is not null) font = new(fontFamily, emSize, style);
        else
        {
            font = new(name, emSize, style);
            Trace.WriteLine($"Using font system font for {name}");
        }

        fonts.Add(identifier, font);
        return font;
    }

    #region IDisposable Support

    bool disposed;
    void Dispose(bool disposing)
    {
        if (!disposed)
        {
            metrics.Dispose();
            shadow.Dispose();
            fonts.Dispose();
            fontFamilies.Dispose();
            fontCollections.Dispose();

            if (disposing)
            {
                recentlyUsedFonts.Clear();
                container = null;

                metrics = null;
                shadow = null;
                fonts = null;
                fontCollections = null;
                fontFamilies = null;
            }

            disposed = true;
        }
    }

    ~TextGenerator() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}