namespace BrewLib.Graphics.Text;

using System;
using System.Collections.Generic;
using System.Numerics;
using SixLabors.ImageSharp;
using Util;

public class TextLayout
{
    public TextLayout(string text, TextFont font, BoxAlignment alignment, Vector2 maxSize)
    {
        var glyphIndex = 0;
        var width = 0f;
        var height = 0f;

        foreach (var textLine in LineBreaker.Split(text, float.Ceiling(maxSize.X), c => font.GetGlyph(c).Width))
        {
            TextLayoutLine line = new(this, height, alignment, Lines.Count == 0);
            foreach (var c in textLine.Span) line.Add(font.GetGlyph(c), glyphIndex++);

            Lines.Add(line);
            width = Math.Max(width, line.Width);
            height += line.Height;
        }

        if (Lines.Count == 0) Lines.Add(new(this, 0, alignment, true));
        var lastLine = Lines[^1];
        if (lastLine.GlyphCount == 0) height += font.LineHeight;
        lastLine.Add(new(null, 0, font.LineHeight), glyphIndex);

        Size = new(width, height);
    }

    public Vector2 Size { get; }

    public List<TextLayoutLine> Lines { get; } = [];

    public void ForTextBounds(int startIndex, int endIndex, Action<RectangleF> action)
    {
        var index = 0;
        foreach (var line in Lines)
        {
            var topLeft = Vector2.Zero;
            var bottomRight = Vector2.Zero;
            var hasBounds = false;

            for (; index < line.Glyphs.Count; ++index)
            {
                var layoutGlyph = line.Glyphs[index];
                if (!hasBounds && startIndex <= index)
                {
                    topLeft = layoutGlyph.Position;
                    hasBounds = true;
                }

                if (index < endIndex) bottomRight = layoutGlyph.Position + layoutGlyph.Glyph.Size;
            }

            if (hasBounds) action(RectangleF.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y));
        }
    }
    public int GetCharacterIndexAt(Vector2 position)
    {
        var index = 0;
        foreach (var line in Lines)
        {
            var lineMatches = position.Y < line.Position.Y + line.Height;
            for (; index < line.Glyphs.Count; ++index)
            {
                var glyph = line.Glyphs[index];
                if (lineMatches && position.X < glyph.Position.X + glyph.Glyph.Width * .5f) return index;
            }

            if (lineMatches) return index - 1;
        }

        return index - 1;
    }
    public int GetCharacterIndexAbove(int index)
    {
        for (var lineIndex = 0; lineIndex < Lines.Count; ++lineIndex)
        {
            var line = Lines[lineIndex];
            if (index < line.GlyphCount)
            {
                if (lineIndex == 0) return 0;

                var previousLine = Lines[lineIndex - 1];
                return previousLine.GetGlyph(Math.Min(index, previousLine.GlyphCount - 1)).Index;
            }

            index -= line.GlyphCount;
        }

        return getLastGlyph().Index;
    }
    public int GetCharacterIndexBelow(int index)
    {
        for (var lineIndex = 0; lineIndex < Lines.Count; ++lineIndex)
        {
            var line = Lines[lineIndex];
            if (index < line.GlyphCount)
            {
                var lastLineIndex = Lines.Count - 1;
                if (lineIndex == lastLineIndex)
                {
                    var lastLine = Lines[lastLineIndex];
                    return lastLine.GetGlyph(lastLine.GlyphCount - 1).Index;
                }

                var nextLine = Lines[lineIndex + 1];
                return nextLine.GetGlyph(Math.Min(index, nextLine.GlyphCount - 1)).Index;
            }

            index -= line.GlyphCount;
        }

        return getLastGlyph().Index;
    }
    public TextLayoutGlyph GetGlyph(int index)
    {
        foreach (var line in Lines)
        {
            if (index < line.GlyphCount) return line.GetGlyph(index);
            index -= line.GlyphCount;
        }

        return getLastGlyph();
    }
    TextLayoutGlyph getLastGlyph()
    {
        var lastLine = Lines[^1];
        return lastLine.GetGlyph(lastLine.GlyphCount - 1);
    }
}

public class TextLayoutLine(TextLayout layout, float y, BoxAlignment alignment, bool advanceOnEmptyGlyph)
{
    bool advance = advanceOnEmptyGlyph;

    public List<TextLayoutGlyph> Glyphs { get; } = [];
    public int GlyphCount => Glyphs.Count;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public Vector2 Position => new((alignment & BoxAlignment.Left) > 0 ? 0 :
        (alignment & BoxAlignment.Right) > 0 ? layout.Size.X - Width : layout.Size.X * .5f - Width * .5f, y);

    public void Add(FontGlyph glyph, int glyphIndex)
    {
        if (!glyph.IsEmpty) advance = true;

        Glyphs.Add(new(this, glyph, glyphIndex, Width));
        if (advance) Width += glyph.Width;
        Height = Math.Max(Height, glyph.Height);
    }

    public TextLayoutGlyph GetGlyph(int index) => Glyphs[index];
}

public readonly record struct TextLayoutGlyph(TextLayoutLine Line, FontGlyph Glyph, int Index, float X)
{
    public Vector2 Position
    {
        get
        {
            var linePosition = Line.Position;
            return linePosition with { X = linePosition.X + X };
        }
    }
}