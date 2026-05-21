namespace BrewLib.Graphics.Text;

using System.Numerics;
using Textures;
using Tiny.PooledCollections.Generic;
using Util;

public sealed class TextFontAtlased(string name, float size, TextureContainer container) : TextFont
{
    readonly PooledDictionary<char, FontGlyph> glyphs = new();

    public string Name => name;
    public float Size => size;
    public int LineHeight => GetGlyph(' ').height;

    public FontGlyph GetGlyph(char c) => glyphs.TryGetValue(c, out var glyph) ? glyph : glyphs[c] = generateGlyph(c);

    FontGlyph generateGlyph(char c)
    {
        Vector2 measuredSize;
        if (char.IsWhiteSpace(c))
        {
            DrawState.TextGenerator.CreateBitmap(c == '\n' ? ['\u200b', c] : [c],
                name,
                size,
                Vector2.Zero,
                BoxAlignment.Centre,
                out measuredSize,
                true);

            return new(null,
                float.ConvertToIntegerNative<int>(measuredSize.X),
                float.ConvertToIntegerNative<int>(measuredSize.Y));
        }

        using var bitmap = DrawState.TextGenerator.CreateBitmap([c],
            name,
            size,
            Vector2.Zero,
            BoxAlignment.Centre,
            out measuredSize,
            false);

        return new(container.Add(bitmap),
            float.ConvertToIntegerNative<int>(measuredSize.X),
            float.ConvertToIntegerNative<int>(measuredSize.Y));
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var glyph in glyphs.Values) glyph.Texture?.Dispose();
        glyphs.Dispose();

        disposed = true;
    }

    #endregion
}