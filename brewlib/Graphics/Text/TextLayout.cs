﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using BrewLib.Util;

namespace BrewLib.Graphics.Text;

public class TextLayout
{
    readonly List<string> textLines = [];
    public IEnumerable<string> TextLines => textLines;

    readonly List<TextLayoutLine> lines = [];
    public IEnumerable<TextLayoutLine> Lines => lines;

    Vector2 size;
    public Vector2 Size => size;

    public IEnumerable<TextLayoutGlyph> Glyphs => lines.SelectMany(line => line.Glyphs);
    public IEnumerable<TextLayoutGlyph> VisibleGlyphs
    {
        get 
        {
            for (var i = 0; i < lines.Count; ++i) foreach (var glyph in lines[i].Glyphs) if (!glyph.Glyph.IsEmpty) yield return glyph; 
        }
    }

    public TextLayout(string text, TextFont font, BoxAlignment alignment, Vector2 maxSize)
    {
        textLines = LineBreaker.Split(text, (int)Math.Ceiling(maxSize.X), c => font.GetGlyph(c).Width);

        var glyphIndex = 0;
        var width = 0f;
        var height = 0f;

        for (var i = 0; i < textLines.Count; ++i)
        {
            TextLayoutLine line = new(this, height, alignment, lines.Count == 0);
            foreach (var c in textLines[i]) line.Add(font.GetGlyph(c), ++glyphIndex);

            lines.Add(line);
            width = Math.Max(width, line.Width);
            height += line.Height;
        }

        if (lines.Count == 0) lines.Add(new(this, 0, alignment, true));
        var lastLine = lines[^1];
        if (lastLine.GlyphCount == 0) height += font.LineHeight;
        lastLine.Add(new(null, 0, font.LineHeight), glyphIndex++);

        size = new(width, height);
    }
    public TextLayoutGlyph GetGlyph(int index)
    {
        foreach (var line in lines)
        {
            if (index < line.GlyphCount) return line.GetGlyph(index);
            index -= line.GlyphCount;
        }
        return getLastGlyph();
    }
    public int GetCharacterIndexAt(Vector2 position)
    {
        var index = 0;
        foreach (var line in lines)
        {
            var lineMatches = position.Y < line.Position.Y + line.Height;
            foreach (var glyph in line.Glyphs)
            {
                if (lineMatches && position.X < glyph.Position.X + glyph.Glyph.Width * 0.5f) return index;
                ++index;
            }
            if (lineMatches) return index - 1;
        }
        return index - 1;
    }
    public void ForTextBounds(int startIndex, int endIndex, Action<RectangleF> action)
    {
        var index = 0;
        for (var i = 0; i < lines.Count; ++i)
        {
            var topLeft = Vector2.Zero;
            var bottomRight = Vector2.Zero;
            var hasBounds = false;

            foreach (var layoutGlyph in lines[i].Glyphs)
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
    public int GetCharacterIndexAbove(int index)
    {
        var lineIndex = 0;
        foreach (var line in lines)
        {
            if (index < line.GlyphCount)
            {
                if (lineIndex == 0) return 0;

                var previousLine = lines[lineIndex - 1];
                return previousLine.GetGlyph(Math.Min(index, previousLine.GlyphCount - 1)).Index;
            }
            index -= line.GlyphCount;
            ++lineIndex;
        }
        return getLastGlyph().Index;
    }
    public int GetCharacterIndexBelow(int index)
    {
        var lineIndex = 0;
        foreach (var line in lines)
        {
            if (index < line.GlyphCount)
            {
                var lastLineIndex = lines.Count - 1;
                if (lineIndex == lastLineIndex)
                {
                    var lastLine = lines[lastLineIndex];
                    return lastLine.GetGlyph(lastLine.GlyphCount - 1).Index;
                }

                var nextLine = lines[lineIndex + 1];
                return nextLine.GetGlyph(Math.Min(index, nextLine.GlyphCount - 1)).Index;
            }
            index -= line.GlyphCount;
            lineIndex++;
        }
        return getLastGlyph().Index;
    }
    TextLayoutGlyph getLastGlyph()
    {
        var lastLine = lines[^1];
        return lastLine.GetGlyph(lastLine.GlyphCount - 1);
    }
}
public class TextLayoutLine(TextLayout layout, float y, BoxAlignment alignment, bool advance)
{
    readonly List<TextLayoutGlyph> glyphs = [];
    public IEnumerable<TextLayoutGlyph> Glyphs => glyphs;
    public int GlyphCount => glyphs.Count;

    int width;
    public int Width => width;

    int height;
    public int Height => height;

    public Vector2 Position => new((alignment & BoxAlignment.Left) > 0 ? 0 : (alignment & BoxAlignment.Right) > 0 ? layout.Size.X - width : layout.Size.X * .5f - width * .5f, y);

    public void Add(FontGlyph glyph, int glyphIndex)
    {
        if (!glyph.IsEmpty) advance = true;

        glyphs.Add(new(this, glyph, glyphIndex, width));
        if (advance) width += glyph.Width;
        height = Math.Max(height, glyph.Height);
    }
    public TextLayoutGlyph GetGlyph(int index) => glyphs[index];
}
public class TextLayoutGlyph(TextLayoutLine line, FontGlyph glyph, int index, float x)
{
    public FontGlyph Glyph => glyph;
    public int Index => index;

    public Vector2 Position => new(line.Position.X + x, line.Position.Y);
}