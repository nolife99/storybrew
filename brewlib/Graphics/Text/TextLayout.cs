namespace BrewLib.Graphics.Text;

using System;
using System.Numerics;
using SixLabors.ImageSharp;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;
using Util;

public struct TextLayout : IDisposable
{
    ValueList<TextLayoutLine> _lines;
    ValueArray<float> size;

    public TextLayout(scoped ReadOnlySpan<char> text, TextFont font, BoxAlignment alignment, Vector2 maxSize)
    {
        var glyphIndex = 0;
        var width = 0f;
        var height = 0f;

        _lines = ValueList.Create<TextLayoutLine>();
        size = ValueArray.Create<float>(2);

        using (var lineBreaks = LineBreaker.Split(text, font, float.Ceiling(maxSize.X), (c, f) => f.GetGlyph(c).width))
            foreach (var (start, length) in lineBreaks)
            {
                TextLayoutLine line = new(size, height, alignment, _lines.Count == 0);

                var span = text.Slice(start, length);
                foreach (var c in span) line.Add(font.GetGlyph(c), c, glyphIndex++);

                _lines.Add(line);
                width = float.Max(width, line.Width);
                height += line.Height;
            }

        if (_lines.Count == 0) _lines.Add(new(size, 0, alignment, true));
        ref var lastLine = ref _lines[^1];
        if (lastLine.GlyphCount == 0) height += font.LineHeight;
        lastLine.Add(new(null, 0, font.LineHeight), '\0', glyphIndex);

        size[0] = width;
        size[1] = height;
    }

    public bool IsValid => _lines.IsValid;

    public readonly Vector2 Size => Vector2.Create(size.AsReadOnlySpan());

    public readonly ReadOnlySpan<TextLayoutLine> Lines => _lines.AsReadOnlySpan();

    public void Dispose()
    {
        foreach (var line in _lines) line.Dispose();
        _lines.Dispose();

        size.Dispose();
    }

    public readonly void ForTextBounds<TState>(int startIndex,
        int endIndex,
        Action<RectangleF, TState> action,
        TState state)
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

            if (hasBounds) action(RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y), state);
        }
    }

    public readonly int GetCharacterIndexAt(Vector2 position)
    {
        var index = 0;
        foreach (var line in _lines)
        {
            var lineMatches = position.Y < line.Position.Y + line.Height;
            foreach (var glyph in line.Glyphs)
            {
                if (lineMatches && position.X < glyph.Position.X + glyph.Glyph.width * .5f) return index;

                ++index;
            }

            if (lineMatches) return index - 1;
        }

        return index - 1;
    }

    public readonly int GetCharacterIndexAbove(int index)
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

    public readonly int GetCharacterIndexBelow(int index)
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

    public readonly TextLayoutGlyph GetGlyph(int index)
    {
        foreach (var line in _lines)
        {
            if (index < line.GlyphCount) return line.GetGlyph(index);

            index -= line.GlyphCount;
        }

        return getLastGlyph();
    }

    readonly TextLayoutGlyph getLastGlyph()
    {
        var lastLine = _lines[^1];
        return lastLine.GetGlyph(lastLine.GlyphCount - 1);
    }
}

public struct TextLayoutLine : IDisposable
{
    ValueList<TextLayoutGlyph> _glyphs;
    ValueArray<int> widthRef;

    bool advance;
    readonly ValueArray<float> layout;
    readonly float y;
    readonly BoxAlignment alignment;

    internal TextLayoutLine(ValueArray<float> layout, float y, BoxAlignment alignment, bool advanceOnEmpty)
    {
        this.layout = layout;
        this.y = y;
        this.alignment = alignment;
        advance = advanceOnEmpty;

        _glyphs = ValueList.Create<TextLayoutGlyph>();
        widthRef = ValueArray.Create<int>(1);
        widthRef[0] = 0;
    }

    public readonly ReadOnlySpan<TextLayoutGlyph> Glyphs => _glyphs.AsReadOnlySpan();

    public readonly int GlyphCount => _glyphs.Count;

    public readonly int Width => widthRef[0];
    public int Height { get; private set; }

    public readonly Vector2 Position
        => new((alignment & BoxAlignment.Left) > 0 ? 0 :
            (alignment & BoxAlignment.Right) > 0 ? layout[0] - Width : layout[0] * .5f - Width * .5f,
            y);

    public void Dispose()
    {
        _glyphs.Dispose();
        widthRef.Dispose();
    }

    internal void Add(FontGlyph glyph, char c, int glyphIndex)
    {
        if (!glyph.IsEmpty) advance = true;

        _glyphs.Add(new((alignment, layout, widthRef), glyph, c, glyphIndex, new(Width, y)));
        if (advance) widthRef[0] += glyph.width;
        if (glyph.height > Height) Height = glyph.height;
    }

    public readonly TextLayoutGlyph GetGlyph(int index) => _glyphs[index];
}

public readonly struct TextLayoutGlyph : IComparable<TextLayoutGlyph>
{
    readonly (BoxAlignment alignment, ValueArray<float> layout, ValueArray<int> widthRef) line;
    readonly Vector2 pos;
    readonly char c;

    public readonly FontGlyph Glyph;
    public readonly int Index;

    internal TextLayoutGlyph((BoxAlignment, ValueArray<float>, ValueArray<int>) line,
        FontGlyph glyph,
        char c,
        int index,
        Vector2 pos)
    {
        this.line = line;
        this.pos = pos;
        this.c = c;

        Glyph = glyph;
        Index = index;
    }

    public Vector2 Position
        => new(((line.alignment & BoxAlignment.Left) > 0 ? 0 :
                (line.alignment & BoxAlignment.Right) > 0 ? line.layout[0] - line.widthRef[0] :
                line.layout[0] * .5f - line.widthRef[0] * .5f) + pos.X,
            pos.Y);

    public int CompareTo(TextLayoutGlyph other) => c.CompareTo(other.c);
}