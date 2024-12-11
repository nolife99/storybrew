namespace BrewLib.Graphics.Drawables;

using System;
using System.Numerics;
using Cameras;
using Renderers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Textures;
using Util;

public sealed class Sprite : Drawable
{
    readonly RenderStates RenderStates = new();
    public Rgba32 Color;
    public float Rotation;
    public ScaleMode ScaleMode = ScaleMode.None;
    public Texture2dRegion Texture;

    public Vector2 MinSize => Vector2.Zero;
    public Vector2 PreferredSize => Texture?.Size ?? Vector2.Zero;

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity)
    {
        if (Texture is null) return;

        var renderer = DrawState.Prepare(drawContext.Get<QuadRenderer>(), camera, RenderStates);
        var color = Color.WithOpacity(opacity);

        var textureX0 = 0f;
        var textureY0 = 0f;
        float textureX1 = Texture.Width, textureY1 = Texture.Height;

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
                for (var y = bounds.Y; y < bounds.Bottom; y += Texture.Height * scale)
                for (var x = bounds.X; x < bounds.Right; x += Texture.Width * scale)
                    renderer.Draw(Texture, x, y, 0, 0, scale, scale, 0, color, 0, 0,
                        Math.Min((bounds.Right - x) / scale, Texture.Width),
                        Math.Min((bounds.Bottom - y) / scale, Texture.Height));

                break;

            default:
                renderer.Draw(Texture, (bounds.X + bounds.Right) * .5f, (bounds.Y + bounds.Bottom) * .5f,
                    (textureX1 - textureX0) * .5f, (textureY1 - textureY0) * .5f, scale, scale, Rotation, color, textureX0,
                    textureY0, textureX1, textureY1); break;
        }
    }

    #region IDisposable Support

    public void Dispose() { }

    #endregion
}