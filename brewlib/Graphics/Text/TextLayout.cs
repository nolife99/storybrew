namespace BrewLib.Graphics.Text;

using System;
using System.Numerics;
using Collections.Pooled;
using SixLabors.ImageSharp;
using Util;

public class TextLayout : IDisposable
{
    readonly PooledList<TextLayoutLine> _lines = new();

    public TextLayout(string text, TextFont font, BoxAlignment alignment, Vector2 maxSize)
    {
        var glyphIndex = 0;
        var width = 0f;
        var height = 0f;

        using (var lineBreaks = LineBreaker.Split(text, font, float.Ceiling(maxSize.X), (c, f) => f.GetGlyph(c).Width))
            foreach (var (start, length) in lineBreaks)
            {
                TextLayoutLine line = new(this, height, alignment, _lines.Count == 0);

                var span = text.AsSpan(start, length);
                foreach (var c in span) line.Add(font.GetGlyph(c), c, glyphIndex++);

                _lines.Add(line);
                width = float.Max(width, line.Width);
                height += line.Height;
            }

        if (_lines.Count == 0) _lines.Add(new(this, 0, alignment, true));
        var lastLine = _lines[^1];
        if (lastLine.GlyphCount == 0) height += font.LineHeight;
        lastLine.Add(new(null, 0, font.LineHeight), '\0', glyphIndex);

        Size = new(width, height);
    }

    public Vector2 Size { get; }

    public IReadOnlyPooledList<TextLayoutLine> Lines => _lines;

    public void Dispose()
    {
        foreach (var line in _lines) line.Dispose();
        _lines.Dispose();
    }

    public void ForTextBounds(int startIndex, int endIndex, Action<RectangleF> action)
    {
        var index = 0;
        foreach (var line in _lines)
        {
            var topLeft = Vector2.Zero;
            var bottomRight = Vector2.Zero;
            var hasBounds = false;

            foreach (var layoutGlyph in line.Glyphs)
            {
                if (!hasBounds && startIndex <= index)
                {
                    topLeft = layoutGlyph.Position;
                    hasBounds = true;
                }

                if (index < endIndex) bottomRight = layoutGlyph.Position + layoutGlyph.Glyph.Size;
                ++index;
            }

            if (hasBounds) action(RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y));
        }
    }

    public int GetCharacterIndexAt(Vector2 position)
    {
        var index = 0;
        foreach (var line in _lines)
        {
            var lineMatches = position.Y < line.Position.Y + line.Height;
            foreach (var glyph in line.Glyphs)
            {
                if (lineMatches && position.X < glyph.Position.X + glyph.Glyph.Width * .5f) return index;

                ++index;
            }

            if (lineMatches) return index - 1;
        }

        return index - 1;
    }

    public int GetCharacterIndexAbove(int index)
    {
        for (var i = 0; i < _lines.Count; ++i)
        {
            var line = _lines[i];
            if (index < line.GlyphCount)
            {
                if (i == 0) return 0;

                var previousLine = _lines[i - 1];
                return previousLine.GetGlyph(int.Min(index, previousLine.GlyphCount - 1)).Index;
            }

            index -= line.GlyphCount;
        }

        return getLastGlyph().Index;
    }

    public int GetCharacterIndexBelow(int index)
    {
        for (var i = 0; i < _lines.Count; ++i)
        {
            var line = _lines[i];
            if (index < line.GlyphCount)
            {
                var lastLineIndex = _lines.Count - 1;
                if (i == lastLineIndex)
                {
                    var lastLine = _lines[lastLineIndex];
                    return lastLine.GetGlyph(lastLine.GlyphCount - 1).Index;
                }

                var nextLine = _lines[i + 1];
                return nextLine.GetGlyph(int.Min(index, nextLine.GlyphCount - 1)).Index;
            }

            index -= line.GlyphCount;
        }

        return getLastGlyph().Index;
    }

    public TextLayoutGlyph GetGlyph(int index)
    {
        foreach (var line in _lines)
        {
            if (index < line.GlyphCount) return line.GetGlyph(index);

            index -= line.GlyphCount;
        }

        return getLastGlyph();
    }

    TextLayoutGlyph getLastGlyph()
    {
        var lastLine = _lines[^1];
        return lastLine.GetGlyph(lastLine.GlyphCount - 1);
    }
}

public class TextLayoutLine(TextLayout layout, float y, BoxAlignment alignment, bool advanceOnEmptyGlyph) : IDisposable
{
    readonly PooledList<TextLayoutGlyph> _glyphs = new();
    bool advance = advanceOnEmptyGlyph, sorted;

    public IReadOnlyPooledList<TextLayoutGlyph> Glyphs
    {
        get
        {
            if (sorted) return _glyphs;

            _glyphs.Sort();
            sorted = true;

            return _glyphs;
        }
    }

    public int GlyphCount => _glyphs.Count;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public Vector2 Position => new((alignment & BoxAlignment.Left) > 0 ? 0 :
        (alignment & BoxAlignment.Right) > 0 ? layout.Size.X - Width : layout.Size.X * .5f - Width * .5f,
        y);

    public void Dispose() => _glyphs.Dispose();

    public void Add(FontGlyph glyph, char character, int glyphIndex)
    {
        if (!glyph.IsEmpty) advance = true;

        _glyphs.Add(new(this, glyph, character, glyphIndex, Width));
        if (advance) Width += glyph.Width;
        if (glyph.Height > Height) Height = glyph.Height;

        sorted = false;
    }

    public TextLayoutGlyph GetGlyph(int index) => _glyphs[index];
}

public readonly record struct TextLayoutGlyph(TextLayoutLine Line, FontGlyph Glyph, char Character, int Index, float X)
    : IComparable<TextLayoutGlyph>
{
    public Vector2 Position
    {
        get
        {
            var linePosition = Line.Position;
            return linePosition with { X = linePosition.X + X };
        }
    }

    public int CompareTo(TextLayoutGlyph other) => Character.CompareTo(other.Character);
}