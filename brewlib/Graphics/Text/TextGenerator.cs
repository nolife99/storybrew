using BrewLib.Data;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using System.Numerics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace BrewLib.Graphics.Text;

public sealed class TextGenerator(ResourceContainer resourceContainer) : IDisposable
{
    SolidBrush textBrush = new(Color.White), shadowBrush = new(Color.FromArgb(220, 0, 0, 0));
    Dictionary<string, Font> fonts = [];
    Dictionary<string, FontFamily> fontFamilies = [];
    Dictionary<string, PrivateFontCollection> fontCollections = [];
    readonly LinkedList<string> recentlyUsedFonts = [];

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
        using var graphics = System.Drawing.Graphics.FromHwnd(0);

        var font = getFont(fontName, 96 / graphics.DpiY * fontSize, FontStyle.Regular);
        var measuredSize = graphics.MeasureString(text, font, new SizeF(maxSize.X, maxSize.Y), stringFormat);

        var width = measuredSize.Width + padding.X * 2 + 1;
        var height = measuredSize.Height + padding.Y * 2 + 1;

        textureSize = new(width, height);
        if (measureOnly) return null;

        Bitmap bitmap = new((int)width, (int)height, PixelFormat.Format32bppArgb);
        try
        {
            using var textGraphics = System.Drawing.Graphics.FromImage(bitmap);
            textGraphics.TextRenderingHint = TextRenderingHint.AntiAlias;

            textGraphics.DrawString(text, font, shadowBrush, new RectangleF(padding.X + 1, padding.Y + 1, width, height), stringFormat);
            textGraphics.DrawString(text, font, textBrush, new RectangleF(padding.X, padding.Y, width, height), stringFormat);
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
        var identifier = $"{name}|{emSize}|{(int)style}";

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
            var bytes = resourceContainer.GetBytes(name, ResourceSource.Embedded);
            if (bytes is not null) unsafe
            {
                try
                {
                    if (!fontCollections.TryGetValue(name, out var fontCollection)) fontCollections.Add(name, fontCollection = new());
                    fixed (void* pinned = &MemoryMarshal.GetArrayDataReference(bytes)) fontCollection.AddMemoryFont((nint)pinned, bytes.Length);

                    if (fontCollection.Families.Length == 1) Trace.WriteLine($"Loaded font {(fontFamily = fontCollection.Families[0]).Name} for {name}");
                    else Trace.WriteLine($"Failed to load font {name}: Expected one family, got {fontCollection.Families.Length}");
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"Failed to load font {name}: {e.Message}");
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
            textBrush.Dispose();
            shadowBrush.Dispose();
            fonts.Dispose();
            fontCollections.Dispose();
            fontFamilies.Dispose();

            textBrush = null;
            shadowBrush = null;
            fonts = null;
            fontCollections = null;
            fontFamilies = null;

            disposed = true;
        }
    }

    #endregion
}