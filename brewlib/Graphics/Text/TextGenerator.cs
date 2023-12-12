using BrewLib.Data;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using System.Numerics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

namespace BrewLib.Graphics.Text;

public sealed class TextGenerator : IDisposable
{
    SolidBrush shadowBrush = new(Color.FromArgb(220, 0, 0, 0));
    System.Drawing.Graphics metrics;
    ResourceContainer container;

    Dictionary<string, Font> fonts = [];
    Dictionary<string, FontFamily> fontFamilies = [];
    Dictionary<string, PrivateFontCollection> fontCollections = [];
    readonly LinkedList<string> recentlyUsedFonts = [];

    public TextGenerator(ResourceContainer resourceContainer)
    {
        metrics = System.Drawing.Graphics.FromHwnd(0);
        metrics.TextRenderingHint = TextRenderingHint.SingleBitPerPixel;
        metrics.InterpolationMode = InterpolationMode.NearestNeighbor;

        container = resourceContainer;
    }

    public Bitmap CreateBitmap(string text, string fontName, float fontSize, Vector2 maxSize, Vector2 padding, BoxAlignment alignment, StringTrimming trimming, out Vector2 textureSize, bool measureOnly)
    {
        if (string.IsNullOrEmpty(text)) text = " ";
        var horizontalAlignment = (alignment & BoxAlignment.Horizontal) switch
        {
            BoxAlignment.Left => StringAlignment.Near,
            BoxAlignment.Right => StringAlignment.Far,
            _ => StringAlignment.Center,
        };
        var verticalAlignment = (alignment & BoxAlignment.Vertical) switch
        {
            BoxAlignment.Top => StringAlignment.Near,
            BoxAlignment.Bottom => StringAlignment.Far,
            _ => StringAlignment.Center,
        };

        using StringFormat stringFormat = new(StringFormat.GenericTypographic)
        {
            Alignment = horizontalAlignment,
            LineAlignment = verticalAlignment,
            Trimming = trimming,
            FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip
        };

        var font = getFont(fontName, 96 / metrics.DpiY * fontSize, FontStyle.Regular);
        var measuredSize = metrics.MeasureString(text, font, new SizeF(maxSize), stringFormat);

        var width = measuredSize.Width + padding.X * 2 + 1;
        var height = measuredSize.Height + padding.Y * 2 + 1;

        textureSize = new(width, height);
        if (measureOnly) return null;

        Bitmap bitmap = new((int)width, (int)height);
        try
        {
            using var textGraphics = System.Drawing.Graphics.FromImage(bitmap);
            textGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            textGraphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            textGraphics.PixelOffsetMode = PixelOffsetMode.Half;

            textGraphics.DrawString(text, font, shadowBrush, new RectangleF(padding.X + 1, padding.Y + 1, width, height), stringFormat);
            textGraphics.DrawString(text, font, Brushes.White, new RectangleF(padding.X, padding.Y, width, height), stringFormat);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
        return bitmap;
    }
    public Texture2d CreateTexture(string text, string fontName, float fontSize, Vector2 maxSize, Vector2 padding, BoxAlignment alignment, StringTrimming trimming, out Vector2 textureSize)
    {
        using var bitmap = CreateBitmap(text, fontName, fontSize, maxSize, padding, alignment, trimming, out textureSize, false);
        return Texture2d.Load(bitmap, $"text:{text}@{fontName}:{fontSize}");
    }
    Font getFont(string name, float emSize, FontStyle style)
    {
        var identifier = $"{name}.{emSize}.{(int)style}";

        if (fonts.TryGetValue(identifier, out var font))
        {
            recentlyUsedFonts.Remove(identifier);
            recentlyUsedFonts.AddFirst(identifier);
            return font;
        }
        else recentlyUsedFonts.AddFirst(identifier);

        if (recentlyUsedFonts.Count > 64) while (recentlyUsedFonts.Count > 32)
        {
            var lastFontIdentifier = recentlyUsedFonts.Last.Value;
            recentlyUsedFonts.RemoveLast();

            fonts[lastFontIdentifier].Dispose();
            fonts.Remove(lastFontIdentifier);
        }

        if (!fontFamilies.TryGetValue(name, out var fontFamily))
        {
            var bytes = container.GetBytes(name, ResourceSource.Embedded);
            if (bytes is not null) unsafe
            {
                try
                {
                    if (!fontCollections.TryGetValue(name, out var fontCollection)) fontCollections.Add(name, fontCollection = new());
                    fixed (void* pinned = &MemoryMarshal.GetArrayDataReference(bytes)) fontCollection.AddMemoryFont((nint)pinned, bytes.Length);

                    var families = fontCollection.Families;
                    if (families.Length == 1) Trace.WriteLine($"Loaded font {(fontFamily = families[0]).Name} for {name}");
                    else
                    {
                        Trace.TraceError($"Failed to load font {name}: Expected one family, got {fontCollection.Families.Length}");
                        if (families is not null) for (var i = 0; i < families.Length; ++i) families[i].Dispose();
                    }
                }
                catch (Exception e)
                {
                    Trace.TraceError($"Failed to load font {name}: {e.Message}");
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
    public void Dispose()
    {
        if (!disposed)
        {
            shadowBrush.Dispose();
            metrics.Dispose();
            fonts.Dispose();
            fontFamilies.Dispose();
            fontCollections.Dispose();

            shadowBrush = null;
            metrics = null;
            fonts = null;
            fontCollections = null;
            fontFamilies = null;
            container = null;

            disposed = true;
        }
    }

    #endregion
}