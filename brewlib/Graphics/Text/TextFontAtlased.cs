namespace BrewLib.Graphics.Text;

using System;
using System.Collections.Generic;
using System.Numerics;
using Textures;
using Util;

public sealed class TextFontAtlased(string name, float size) : TextFont
{
    TextureMultiAtlas2d atlas;
    Dictionary<char, FontGlyph> glyphs = [];

    public string Name => name;
    public float Size => size;
    public int LineHeight => GetGlyph(' ').Height;

    public FontGlyph GetGlyph(char c)
    {
        if (!glyphs.TryGetValue(c, out var glyph)) glyphs[c] = glyph = generateGlyph(c);
        return glyph;
    }

    FontGlyph generateGlyph(char c)
    {
        Vector2 measuredSize;
        if (char.IsWhiteSpace(c))
        {
            DrawState.TextGenerator.CreateBitmap(c.ToString(), name, size, default, BoxAlignment.Centre, out measuredSize, true);

            return new(null, (int)measuredSize.X, (int)measuredSize.Y);
        }

        atlas ??= new(512, 512, $"Font Atlas {name}:{size:n1}");
        using var bitmap = DrawState.TextGenerator.CreateBitmap(c.ToString(), name, size, default, BoxAlignment.Centre,
            out measuredSize, false);

        return new(atlas.AddRegion(bitmap, $"{Convert.ToInt32(c)}{Name}{Size:n1}"), (int)measuredSize.X, (int)measuredSize.Y);
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;
        foreach (var glyph in glyphs.Values) glyph.Texture?.Dispose();
        atlas?.Dispose();

        disposed = true;
    }

    #endregion
}