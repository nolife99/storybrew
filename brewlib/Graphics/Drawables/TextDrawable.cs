using System;
using System.Drawing;
using System.Numerics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Text;
using BrewLib.Util;

namespace BrewLib.Graphics.Drawables;

public sealed class TextDrawable : Drawable
{
    TextLayout textLayout;

    TextFont font;
    float currentFontSize, currentScaling = 1;

    public Vector2 MinSize => Size;
    public Vector2 PreferredSize => Size;
    public Vector2 Size
    {
        get
        {
            validate();
            return text?.Length > 0 ? textLayout.Size / scaling : font.GetGlyph(' ').Size / scaling;
        }
    }

    string text = "";
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

    public string Icon 
    { 
        get => text; 
        set => Text = value; 
    }

    string fontName = "Tahoma";
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

    float fontSize = 12;
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

    Vector2 maxSize;
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

    float scaling = 1;
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

    BoxAlignment alignment = BoxAlignment.TopLeft;
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

    StringTrimming trimming = StringTrimming.None;
    public StringTrimming Trimming
    {
        get => trimming;
        set
        {
            if (trimming == value) return;
            trimming = value;
            invalidate();
        }
    }

    public readonly RenderStates RenderStates = new();
    public Color Color = Color.White;

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity)
    {
        validate();

        var inverseScaling = 1 / scaling;
        var color = Color.WithOpacity(opacity);

        var renderer = DrawState.Prepare(drawContext.Get<QuadRenderer>(), camera, RenderStates);

        var clipRegion = DrawState.GetClipRegion(camera) ?? new(
            new(camera.ExtendedViewport.Left + camera.Position.X, camera.ExtendedViewport.Top + camera.Position.Y), camera.ExtendedViewport.Size);

        foreach (var layoutGlyph in textLayout.VisibleGlyphs)
        {
            var glyph = layoutGlyph.Glyph;
            var position = layoutGlyph.Position;

            var y = bounds.Top + position.Y * inverseScaling;
            var height = glyph.Height * inverseScaling;

            if (y > clipRegion.Bottom) break;
            if (y + height < clipRegion.Top) continue;

            var x = bounds.Left + position.X * inverseScaling;

            renderer.Draw(glyph.Texture, x, y, 0, 0, inverseScaling, inverseScaling, 0, color);
        }
    }
    public RectangleF GetCharacterBounds(int index)
    {
        validate();

        var inverseScaling = 1 / scaling;
        var layoutGlyph = textLayout.GetGlyph(index);
        var glyph = layoutGlyph.Glyph;
        var position = layoutGlyph.Position * inverseScaling;

        return new(position.X, position.Y, glyph.Width * inverseScaling, glyph.Height * inverseScaling);
    }
    public int GetCharacterIndexAt(Vector2 position)
    {
        validate();
        return textLayout.GetCharacterIndexAt(position * scaling);
    }
    public void ForTextBounds(int startIndex, int endIndex, Action<RectangleF> action)
    {
        validate();
        var inverseScaling = 1 / scaling;
        textLayout.ForTextBounds(startIndex, endIndex, bounds => action(RectangleF.FromLTRB(
            bounds.Left * inverseScaling, bounds.Top * inverseScaling, bounds.Right * inverseScaling, bounds.Bottom * inverseScaling)));
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
        if (font is null || font.Name != FontName || currentFontSize != FontSize || currentScaling != Scaling)
        {
            font?.Dispose();
            font = DrawState.TextFontManager.GetTextFont(FontName, FontSize, Scaling);

            currentFontSize = FontSize;
            currentScaling = Scaling;
        }

        textLayout = new(text ?? "", font, alignment, MaxSize * scaling);
    }

    public void Dispose()
    {
        font?.Dispose();
        font = null;
    }
}