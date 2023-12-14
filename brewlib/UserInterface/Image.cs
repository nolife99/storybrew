﻿using System.Numerics;
using BrewLib.Graphics;
using BrewLib.Graphics.Drawables;
using BrewLib.Graphics.Textures;
using BrewLib.UserInterface.Skinning.Styles;

namespace BrewLib.UserInterface;

public class Image(WidgetManager manager) : Widget(manager)
{
    Sprite sprite = new();

    public override Vector2 PreferredSize => sprite.Texture?.Size ?? Vector2.Zero;
    public Texture2dRegion Texture
    {
        get => sprite.Texture;
        set => sprite.Texture = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) sprite?.Dispose();
        sprite = null;

        base.Dispose(disposing);
    }

    protected override WidgetStyle Style => Manager.Skin.GetStyle<ImageStyle>(StyleName);
    protected override void ApplyStyle(WidgetStyle style)
    {
        base.ApplyStyle(style);
        var imageStyle = (ImageStyle)style;

        sprite.Color = imageStyle.Color;
        sprite.ScaleMode = imageStyle.ScaleMode;
    }
    protected override void DrawBackground(DrawContext drawContext, float actualOpacity)
    {
        base.DrawBackground(drawContext, actualOpacity);
        sprite.Draw(drawContext, Manager.Camera, Bounds, actualOpacity);
    }
}