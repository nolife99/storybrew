using BrewLib.Data;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
using osuTK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Bitmap = System.Drawing.Bitmap;

namespace BrewLib.Graphics.Text
{
    public class TextGenerator : IDisposable
    {
        SolidBrush textBrush = new(Color.White), shadowBrush = new(Color.FromArgb(220, 0, 0, 0));
        Dictionary<string, Font> fonts = new();
        Dictionary<string, FontFamily> fontFamilies = new();
        Dictionary<string, PrivateFontCollection> fontCollections = new();
        readonly LinkedList<string> recentlyUsedFonts = new();
        readonly ResourceContainer resourceContainer;
        public TextGenerator(ResourceContainer resourceContainer) => this.resourceContainer = resourceContainer;

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
            using var graphics = System.Drawing.Graphics.FromHwnd(default);
            using var stringFormat = new StringFormat(StringFormat.GenericTypographic);
            graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            stringFormat.Alignment = horizontalAlignment;
            stringFormat.LineAlignment = verticalAlignment;
            stringFormat.Trimming = trimming;
            stringFormat.FormatFlags = StringFormatFlags.FitBlackBox | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip;

            var dpiScale = 96 / graphics.DpiY;
            var font = getFont(fontName, fontSize * dpiScale, FontStyle.Regular);

            var measuredSize = graphics.MeasureString(text, font, new SizeF(maxSize.X, maxSize.Y), stringFormat);
            var width = (int)(measuredSize.Width + padding.X * 2 + 1);
            var height = (int)(measuredSize.Height + padding.Y * 2 + 1);

            var offsetX = padding.X;
            var offsetY = padding.Y;

            textureSize = new Vector2(width, height);
            if (measureOnly) return null;

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            try
            {
                using var textGraphics = System.Drawing.Graphics.FromImage(bitmap);
                textGraphics.TextRenderingHint = graphics.TextRenderingHint;
                textGraphics.SmoothingMode = SmoothingMode.None;
                textGraphics.InterpolationMode = InterpolationMode.High;

                textGraphics.DrawString(text, font, shadowBrush, new RectangleF(offsetX + 1, offsetY + 1, width, height), stringFormat);
                textGraphics.DrawString(text, font, textBrush, new RectangleF(offsetX, offsetY, width, height), stringFormat);
            }
            catch (Exception)
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

            if (fonts.TryGetValue(identifier, out Font font))
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

            if (!fontFamilies.TryGetValue(name, out FontFamily fontFamily))
            {
                var bytes = resourceContainer.GetBytes(name, ResourceSource.Embedded);
                if (bytes != null)
                {
                    var pinnedArray = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    try
                    {
                        if (!fontCollections.TryGetValue(name, out PrivateFontCollection fontCollection))
                            fontCollections.Add(name, fontCollection = new PrivateFontCollection());

                        fontCollection.AddMemoryFont(bytes.AddrOfPinnedArray(), bytes.Length);

                        if (fontCollection.Families.Length == 1)
                        {
                            fontFamily = fontCollection.Families[0];
                            Trace.WriteLine($"Loaded font {fontFamily.Name} for {name}");
                        }
                        else Trace.WriteLine($"Failed to load font {name}: Expected one family, got {fontCollection.Families.Length}");
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine($"Failed to load font {name}: {e.Message}");
                    }
                    finally
                    {
                        pinnedArray.Free();
                    }
                }
                fontFamilies.Add(name, fontFamily);
            }

            if (fontFamily != null) font = new Font(fontFamily, emSize, style);
            else
            {
                font = new Font(name, emSize, style);
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
                if (disposing)
                {
                    textBrush.Dispose();
                    shadowBrush.Dispose();
                    foreach (var entry in fonts) entry.Value.Dispose();
                    foreach (var fontCollection in fontCollections.Values) fontCollection.Dispose();
                    fonts.Clear();
                    fontCollections.Clear();
                }

                textBrush = null;
                shadowBrush = null;
                fonts = null;
                fontCollections = null;
                fontFamilies = null;

                disposed = true;
            }
        }
        public void Dispose() => Dispose(true);

        #endregion
    }
}