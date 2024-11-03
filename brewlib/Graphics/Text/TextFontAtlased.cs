using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using BrewLib.Graphics.Textures;
using BrewLib.Util;

namespace BrewLib.Graphics.Text;

public class TextFontAtlased(string name, float size) : TextFont
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
            DrawState.TextGenerator.CreateBitmap(c.ToString(), name, size, SizeF.Empty, Vector2.Zero, BoxAlignment.Centre, StringTrimming.None, out measuredSize, true);
            return new(null, (int)measuredSize.X, (int)measuredSize.Y);
        }
        else
        {
            atlas ??= new(512, 512, $"FontAtlas {name} {size}x");
            using var bitmap = DrawState.TextGenerator.CreateBitmap(c.ToString(), name, size, SizeF.Empty, Vector2.Zero, BoxAlignment.Centre, StringTrimming.None, out measuredSize, false);
            return new(atlas.AddRegion(bitmap, $"{c}{Name}{Size:n1}"), (int)measuredSize.X, (int)measuredSize.Y);
        }
    }

    #region IDisposable Support

    bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                foreach (var glyph in glyphs.Values) glyph.Texture?.Dispose();
                atlas?.Dispose();
            }
            glyphs = null;
            atlas = null;
            disposed = true;
        }
    }
    public void Dispose() => Dispose(true);

    #endregion
}