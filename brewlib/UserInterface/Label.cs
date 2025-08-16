namespace BrewLib.UserInterface;

using System;
using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Drawables;
using BrewLib.UserInterface.Skinning.Styles;
using BrewLib.Util;
using SixLabors.ImageSharp;

public class Label(WidgetManager manager) : Widget(manager)
{
    readonly TextDrawable textDrawable = new();

    public override Vector2 MinSize => new(0, PreferredSize.Y);

    public override Vector2 PreferredSize => textDrawable.Size;

    public ReadOnlySpan<char> Text
    {
        get => textDrawable.Text;
        set
        {
            if (textDrawable.Text.SequenceEqual(value)) return;

            textDrawable.Text = value;
            InvalidateAncestorLayout();
        }
    }

    public IconFont Icon
    {
        get => textDrawable.Icon;
        set
        {
            if (textDrawable.Icon == value) return;

            textDrawable.Icon = value;
            InvalidateAncestorLayout();
        }
    }

    public RectangleF TextBounds
    {
        get
        {
            var position = AbsolutePosition;
            var size = Size;
            Vector2 textSize = new(float.Min(textDrawable.Size.X, size.X), float.Min(textDrawable.Size.Y, size.Y));

            var alignment = textDrawable.Alignment;
            if ((alignment & BoxAlignment.Right) > 0) position.X += size.X - textSize.X;
            else if ((alignment & BoxAlignment.Left) == 0) position.X += size.X * .5f - textSize.X * .5f;

            if ((alignment & BoxAlignment.Bottom) > 0) position.Y += size.Y - textSize.Y;
            else if ((alignment & BoxAlignment.Top) == 0) position.Y += size.Y * .5f - textSize.Y * .5f;

            position = Manager.SnapToPixel(position);
            return RectangleF.FromLTRB(position.X, position.Y, textSize.X, textSize.Y);
        }
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<LabelStyle>(StyleName);

    protected override void Dispose(bool disposing)
    {
        if (disposing) textDrawable.Dispose();
        base.Dispose(disposing);
    }

    protected override void ApplyStyle(WidgetStyle style)
    {
        base.ApplyStyle(style);
        var labelStyle = (LabelStyle)style;

        textDrawable.FontName = labelStyle.FontName;
        textDrawable.FontSize = labelStyle.FontSize;
        textDrawable.Alignment = labelStyle.TextAlignment;
        textDrawable.Color = labelStyle.Color;
    }

    public override void PreLayout()
    {
        base.PreLayout();

        var scalingChanged = false;

        var scaling = (Manager.Camera as CameraOrtho)?.HeightScaling ?? 1;
        if (scaling != 0 && textDrawable.Scaling != scaling)
        {
            textDrawable.Scaling = scaling;
            scalingChanged = true;
        }

        if (!NeedsLayout && !scalingChanged) return;

        textDrawable.MaxSize = Vector2.Zero;
        InvalidateAncestorLayout();
    }

    protected override void Layout()
    {
        base.Layout();
        if (textDrawable.MaxSize == Size) return;

        textDrawable.MaxSize = Size;
        InvalidateAncestorLayout();
    }

    protected override void DrawBackground(DrawContext drawContext, float actualOpacity)
    {
        base.DrawBackground(drawContext, actualOpacity);
        if (!textDrawable.Text.IsWhiteSpace())
            textDrawable.Draw(drawContext, Manager.Camera, TextBounds, actualOpacity);
    }

    public RectangleF GetCharacterBounds(int index)
        => RectangleF.Transform(textDrawable.GetCharacterBounds(index), Matrix3x2.CreateTranslation(AbsolutePosition));

    public void ForTextBounds<TState>(int startIndex, int endIndex, Action<RectangleF, TState> action, TState state)
        => textDrawable.ForTextBounds(startIndex,
            endIndex,
            (bounds, a) => a.action(RectangleF.Transform(bounds, Matrix3x2.CreateTranslation(a.AbsolutePosition)),
                a.state),
            (action, AbsolutePosition, state));

    public int GetCharacterIndexAt(Vector2 position) => textDrawable.GetCharacterIndexAt(position - AbsolutePosition);
    public int GetCharacterIndexAbove(int index) => textDrawable.GetCharacterIndexAbove(index);
    public int GetCharacterIndexBelow(int index) => textDrawable.GetCharacterIndexBelow(index);
}