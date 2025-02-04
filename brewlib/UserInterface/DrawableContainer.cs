﻿namespace BrewLib.UserInterface;

using System.Numerics;
using Graphics;
using Graphics.Drawables;

public class DrawableContainer(WidgetManager manager) : Widget(manager)
{
    Drawable drawable = NullDrawable.Instance;
    public override Vector2 MinSize => drawable?.MinSize ?? Vector2.Zero;

    public override Vector2 PreferredSize => drawable?.PreferredSize ?? Vector2.Zero;

    public Drawable Drawable
    {
        get => drawable;
        set
        {
            if (drawable == value) return;

            drawable = value;
            InvalidateAncestorLayout();
        }
    }

    protected override void DrawBackground(DrawContext drawContext, float actualOpacity)
    {
        base.DrawBackground(drawContext, actualOpacity);
        drawable?.Draw(drawContext, Manager.Camera, Bounds, actualOpacity);
    }
}