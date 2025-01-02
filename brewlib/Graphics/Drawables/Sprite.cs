namespace BrewLib.Graphics.Drawables;

using System.Numerics;
using Cameras;
using Renderers;
using SixLabors.ImageSharp;
using Textures;
using Util;

public sealed class Sprite : Drawable
{
    readonly RenderStates RenderStates = new();
    public Color Color;
    public float Rotation;
    public ScaleMode ScaleMode = ScaleMode.None;
    public Texture2dRegion Texture;

    public Vector2 MinSize => Vector2.Zero;
    public Vector2 PreferredSize => Texture?.Size ?? Vector2.Zero;

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity)
    {
        if (Texture is null) return;

        var renderer = DrawState.Prepare(drawContext.Get<IQuadRenderer>(), camera, RenderStates);

        var color = Color.WithOpacity(opacity);

        var texture0 = Vector2.Zero;
        var texture1 = Texture.Size;
        var scaleVec = new Vector2(bounds.Width, bounds.Height) / texture1;

        float scale;
        switch (ScaleMode)
        {
            case ScaleMode.Fill:
                if (scaleVec.X > scaleVec.Y)
                {
                    scale = scaleVec.X;
                    texture0.Y = (Texture.Height - bounds.Height / scale) * .5f;
                    texture1.Y = Texture.Height - texture0.Y;
                }
                else
                {
                    scale = scaleVec.Y;
                    texture0.X = (Texture.Width - bounds.Width / scale) * .5f;
                    texture1.X = Texture.Width - texture0.X;
                }

                break;

            case ScaleMode.Fit:
            case ScaleMode.RepeatFit: scale = float.Min(scaleVec.X, scaleVec.Y); break;

            default: scale = 1; break;
        }

        switch (ScaleMode)
        {
            case ScaleMode.Repeat:
            case ScaleMode.RepeatFit:
                for (var y = bounds.Y; y < bounds.Bottom; y += Texture.Height * scale)
                for (var x = bounds.X; x < bounds.Right; x += Texture.Width * scale)
                    renderer.Draw(Texture,
                        new(x, y),
                        Vector2.Zero,
                        new(scale),
                        0,
                        color,
                        Vector2.Zero,
                        Vector2.Min(new Vector2(bounds.Right - x, bounds.Bottom - y) / scale, Texture.Size));

                break;

            default:
                renderer.Draw(Texture,
                    ((Vector2)bounds.Location + new Vector2(bounds.Right, bounds.Bottom)) * .5f,
                    (texture1 - texture0) * .5f,
                    new(scale),
                    Rotation,
                    color,
                    texture0,
                    texture1); break;
        }
    }

    #region IDisposable Support

    public void Dispose() { }

    #endregion
}