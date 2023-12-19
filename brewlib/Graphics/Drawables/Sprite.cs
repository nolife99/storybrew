﻿using System;
using System.Drawing;
using System.Numerics;
using BrewLib.Graphics.Cameras;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Textures;
using BrewLib.Util;

namespace BrewLib.Graphics.Drawables;

public class Sprite : Drawable
{
    public Texture2dRegion Texture;
    public RenderStates RenderStates { get; private set; } = new();
    public float Rotation;
    public Color Color = Color.White;
    public ScaleMode ScaleMode = ScaleMode.None;

    public Vector2 MinSize => Vector2.Zero;
    public Vector2 PreferredSize => Texture?.Size ?? Vector2.Zero;

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity)
    {
        if (Texture is null) return;

        var renderer = DrawState.Prepare(drawContext.Get<QuadRenderer>(), camera, RenderStates);
        var color = Color.WithOpacity(opacity);

        var textureX0 = 0f;
        var textureY0 = 0f;
        var textureX1 = Texture.Width;
        var textureY1 = Texture.Height;

        var scaleH = bounds.Width / textureX1;
        var scaleV = bounds.Height / textureY1;

        float scale;
        switch (ScaleMode)
        {
            case ScaleMode.Fill:
                if (scaleH > scaleV)
                {
                    scale = scaleH;
                    textureY0 = (Texture.Height - bounds.Height / scale) * .5f;
                    textureY1 = Texture.Height - textureY0;
                }
                else
                {
                    scale = scaleV;
                    textureX0 = (Texture.Width - bounds.Width / scale) * .5f;
                    textureX1 = Texture.Width - textureX0;
                }
                break;

            case ScaleMode.Fit:
            case ScaleMode.RepeatFit: scale = Math.Min(scaleH, scaleV); break;
            default: scale = 1; break;
        }
        switch (ScaleMode)
        {
            case ScaleMode.Repeat:
            case ScaleMode.RepeatFit:
                for (var y = bounds.Top; y < bounds.Bottom; y += Texture.Height * scale) for (var x = bounds.Left; x < bounds.Right; x += Texture.Width * scale)
                {
                    var textureX = Math.Min((bounds.Right - x) / scale, Texture.Width);
                    var textureY = Math.Min((bounds.Bottom - y) / scale, Texture.Height);
                    renderer.Draw(Texture, x, y, 0, 0, scale, scale, 0, color, 0, 0, textureX, textureY);
                }
                break;

            default:
                renderer.Draw(Texture, (bounds.Left + bounds.Right) * .5f, (bounds.Top + bounds.Bottom) * .5f,
                (textureX1 - textureX0) / 2, (textureY1 - textureY0) * .5f,
                scale, scale, Rotation, color, textureX0, textureY0, textureX1, textureY1);
                break;
        }
    }

    #region IDisposable Support

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        Texture = null;
        RenderStates = null;
    }

    #endregion
}