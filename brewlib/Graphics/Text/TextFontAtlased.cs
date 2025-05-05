namespace BrewLib.Graphics.Text;

using System.Numerics;
using Collections.Pooled;
using Textures;
using Util;

public sealed class TextFontAtlased(string name, float size) : TextFont
{
    readonly PooledDictionary<char, FontGlyph> glyphs = new();

    public string Name => name;
    public float Size => size;
    public int LineHeight => GetGlyph(' ').Height;

    public FontGlyph GetGlyph(char c)
    {
        if (glyphs.TryGetValue(c, out var glyph)) return glyph;

        return glyphs[c] = generateGlyph(c);
    }

    FontGlyph generateGlyph(char c)
    {
        Vector2 measuredSize;
        if (char.IsWhiteSpace(c))
        {
            var prepended = c == '\n' ? "\u200b\n" : c.ToString();
            DrawState.TextGenerator.CreateBitmap(prepended,
                name,
                size,
                default,
                BoxAlignment.Centre,
                out measuredSize,
                true);

            return new(null, (int)measuredSize.X, (int)measuredSize.Y);
        }

        using var bitmap = DrawState.TextGenerator.CreateBitmap(c.ToString(),
            name,
            size,
            default,
            BoxAlignment.Centre,
            out measuredSize,
            false);

        return new(Texture2d.Load(bitmap), (int)measuredSize.X, (int)measuredSize.Y);
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