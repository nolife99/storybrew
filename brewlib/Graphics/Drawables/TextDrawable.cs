namespace BrewLib.Graphics.Drawables;

using System;
using System.Numerics;
using Cameras;
using Renderers;
using SixLabors.ImageSharp;
using Text;
using Util;

public sealed class TextDrawable : Drawable
{
    readonly RenderStates RenderStates = new();
    BoxAlignment alignment = BoxAlignment.TopLeft;
    public Color Color;
    float currentFontSize, currentScaling = 1, fontSize = 12, scaling = 1;

    TextFont font;

    string fontName = "Tahoma", text = "";

    Vector2 maxSize;
    TextLayout textLayout;

    public Vector2 Size
    {
        get
        {
            validate();
            return text?.Length > 0 ? textLayout.Size / scaling : font.GetGlyph(' ').Size / scaling;
        }
    }

    public Vector2 MaxSize
    {
        get => maxSize;
        set
        {
            if (maxSize == value) return;

            maxSize = value;
            invalidate();
        }
    }

    public IconFont Icon { get => text.Length == 0 ? 0 : (IconFont)text[0]; set => text = char.ToString((char)value); }

    public string Text
    {
        get => text;
        set
        {
            if (text == value) return;

            text = value;
            invalidate();
        }
    }

    public string FontName
    {
        get => fontName;
        set
        {
            if (fontName == value) return;

            fontName = value;
            invalidate();
        }
    }

    public float FontSize
    {
        get => fontSize;
        set
        {
            if (fontSize == value) return;

            fontSize = value;
            invalidate();
        }
    }

    public float Scaling
    {
        get => scaling;
        set
        {
            if (scaling == value) return;

            scaling = value;
            invalidate();
        }
    }

    public BoxAlignment Alignment
    {
        get => alignment;
        set
        {
            if (alignment == value) return;

            alignment = value;
            invalidate();
        }
    }

    public Vector2 MinSize => Size;
    public Vector2 PreferredSize => Size;

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity)
    {
        validate();

        var inverseScaling = 1 / scaling;
        var color = Color.WithOpacity(opacity);

        var renderer = DrawState.Prepare(drawContext.Get<IQuadRenderer>(), camera, RenderStates);

        var clipRegion = DrawState.GetClipRegion(camera) ??
            new(
                new(camera.ExtendedViewport.X + camera.Position.X, camera.ExtendedViewport.Y + camera.Position.Y),
                camera.ExtendedViewport.Size);

        foreach (var line in textLayout.Lines)
        foreach (var layoutGlyph in line.Glyphs)
        {
            var glyph = layoutGlyph.Glyph;
            if (glyph.IsEmpty) continue;

            var position = layoutGlyph.Position;
            var y = bounds.Y + position.Y * inverseScaling;
            if (y > clipRegion.Bottom) break;

            if (y + glyph.Height * inverseScaling < clipRegion.Y) continue;

            var texture = glyph.Texture;
            renderer.Draw(
                texture,
                bounds.Left + position.X * inverseScaling,
                y,
                0,
                0,
                inverseScaling,
                inverseScaling,
                0,
                color,
                0,
                0,
                texture.Width,
                texture.Height);
        }
    }

    public void Dispose() => font?.Dispose();

    public RectangleF GetCharacterBounds(int index)
    {
        validate();

        var inverseScaling = 1 / scaling;
        var layoutGlyph = textLayout.GetGlyph(index);
        var glyph = layoutGlyph.Glyph;
        var position = layoutGlyph.Position * inverseScaling;

        return new(position.X, position.Y, glyph.Width * inverseScaling, glyph.Height * inverseScaling);
    }

    public void ForTextBounds(int startIndex, int endIndex, Action<RectangleF> action)
    {
        validate();
        var inverseScaling = 1 / scaling;
        textLayout.ForTextBounds(
            startIndex,
            endIndex,
            bounds => action(
                RectangleF.FromLTRB(
                    bounds.X * inverseScaling,
                    bounds.Y * inverseScaling,
                    bounds.Right * inverseScaling,
                    bounds.Bottom * inverseScaling)));
    }

    public int GetCharacterIndexAt(Vector2 position)
    {
        validate();
        return textLayout.GetCharacterIndexAt(position * scaling);
    }

    public int GetCharacterIndexAbove(int index)
    {
        validate();
        return textLayout.GetCharacterIndexAbove(index);
    }

    public int GetCharacterIndexBelow(int index)
    {
        validate();
        return textLayout.GetCharacterIndexBelow(index);
    }

    void invalidate() => textLayout = null;

    void validate()
    {
        if (textLayout is not null) return;

        if (font is null || font.Name != fontName || currentFontSize != fontSize || currentScaling != scaling)
        {
            font?.Dispose();
            font = DrawState.TextFontManager.GetTextFont(fontName, fontSize, scaling);

            currentFontSize = fontSize;
            currentScaling = scaling;
        }

        textLayout = new(text ?? "", font, alignment, maxSize * scaling);
    }
}