using BrewLib.Graphics.Textures;
using BrewLib.Util;
using System.Numerics;
using System.Collections.Generic;
using System.Drawing;

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
            atlas ??= new(512, 512, $"Font Atlas {name} {size}x");
            using var bitmap = DrawState.TextGenerator.CreateBitmap(c.ToString(), name, size, Vector2.Zero, Vector2.Zero, BoxAlignment.Centre, StringTrimming.None, out measuredSize, false);
            var texture = atlas.AddRegion(bitmap, $"glyph:{c}@{Name}:{Size}");
            return new(texture, (int)measuredSize.X, (int)measuredSize.Y);
        }
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            foreach (var glyph in glyphs.Values) glyph.Texture?.Dispose();
            glyphs.Clear();
            atlas?.Dispose();

            glyphs = null;
            atlas = null;
            disposed = true;
        }
    }

    #endregion
}