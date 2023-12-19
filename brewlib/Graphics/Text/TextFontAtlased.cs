using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using BrewLib.Graphics.Textures;
using BrewLib.Util;

namespace BrewLib.Graphics.Text;

public sealed class TextFontAtlased(string name, float size) : TextFont
{
    Dictionary<char, FontGlyph> glyphs = [];
    TextureMultiAtlas2d atlas;

    public string Name => name;
    public float Size => size;
    public int LineHeight => GetGlyph(' ').Height;

    public FontGlyph GetGlyph(char c)
    {
        if (!glyphs.TryGetValue(c, out var glyph)) glyphs.Add(c, glyph = generateGlyph(c));
        return glyph;
    }
    FontGlyph generateGlyph(char c)
    {
        Vector2 measuredSize;
        if (char.IsWhiteSpace(c))
        {
            DrawState.TextGenerator.CreateBitmap(c.ToString(), name, size, Vector2.Zero, Vector2.Zero, BoxAlignment.Centre, StringTrimming.None, out measuredSize, true);
            return new(null, (int)measuredSize.X, (int)measuredSize.Y);
        }
        else
        {
            atlas ??= new(512, 512, $"Font atlas {name} {size}x");
            using var bitmap = DrawState.TextGenerator.CreateBitmap(c.ToString(), name, size, Vector2.Zero, Vector2.Zero, BoxAlignment.Centre, StringTrimming.None, out measuredSize, false);
            return new(atlas.AddRegion(bitmap, $"glyph:{c}@{Name}:{Size}"), (int)measuredSize.X, (int)measuredSize.Y);
        }
    }

    #region IDisposable Support

    ~TextFontAtlased() => Dispose(false);

    bool disposed;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        if (!disposed)
        {
            foreach (var glyph in glyphs) glyph.Value.Texture?.Dispose();
            glyphs.Clear();
            atlas?.Dispose();

            if (disposing)
            {
                glyphs = null;
                atlas = null;
                disposed = true;
            }
        }
    }

    #endregion
}