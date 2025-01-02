namespace BrewLib.Graphics.Drawables;

using System.Numerics;
using Cameras;
using Renderers;
using SixLabors.ImageSharp;
using Textures;
using Util;

public sealed class NinePatch : Drawable
{
    public FourSide Borders, Outset;
    public bool BordersOnly;
    public Color Color;
    public Texture2dRegion Texture;
    public RenderStates RenderStates { get; } = new();

    public Vector2 PreferredSize => MinSize;

    public Vector2 MinSize => Texture is not null ?
        new Vector2(Borders.Left + Texture.Width - Borders.Right - Outset.Horizontal,
            Borders.Top + Texture.Height - Borders.Bottom - Outset.Vertical) :
        Vector2.Zero;

    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity)
    {
        if (Texture is null) return;

        var vec0 = (Vector2)bounds.Location - new Vector2(Outset.Left, Outset.Top);
        var vec1 = vec0 + new Vector2(Borders.Left, Borders.Top);
        var vec2 = new Vector2(bounds.Right + Outset.Right, bounds.Bottom + Outset.Bottom) -
            new Vector2(Texture.Width - Borders.Right, Texture.Height - Borders.Bottom);

        var scale = (vec2 - vec1) / new Vector2(Borders.Right - Borders.Left, Borders.Bottom - Borders.Top);

        var color = Color.WithOpacity(opacity);
        var renderer = DrawState.Prepare(drawContext.Get<IQuadRenderer>(), camera, RenderStates);

        // Center
        if (!BordersOnly && scale is { X: > 0, Y: > 0 })
            renderer.Draw(Texture,
                vec1,
                Vector2.Zero,
                scale,
                0,
                color,
                new(Borders.Left, Borders.Top),
                new(Borders.Right, Borders.Bottom));

        // Sides
        if (scale.Y > 0)
        {
            var unitX = scale with { X = 1 };
            renderer.Draw(Texture,
                new(vec0.X, vec1.Y),
                Vector2.Zero,
                unitX,
                0,
                color,
                new(0, Borders.Top),
                new(Borders.Left, Borders.Bottom));

            renderer.Draw(Texture,
                new(vec2.X, vec1.Y),
                Vector2.Zero,
                unitX,
                0,
                color,
                new(Borders.Right, Borders.Top),
                new(Texture.Width, Borders.Bottom));
        }

        if (scale.X > 0)
        {
            var unitY = scale with { Y = 1 };
            renderer.Draw(Texture,
                new(vec1.X, vec0.Y),
                Vector2.Zero,
                unitY,
                0,
                color,
                new(Borders.Left, 0),
                new(Borders.Right, Borders.Top));

            renderer.Draw(Texture,
                new(vec1.X, vec2.Y),
                Vector2.Zero,
                unitY,
                0,
                color,
                new(Borders.Left, Borders.Bottom),
                new(Borders.Right, Texture.Height));
        }

        // Corners
        renderer.Draw(Texture, vec0, Vector2.Zero, Vector2.One, 0, color, Vector2.Zero, new(Borders.Left, Borders.Top));

        renderer.Draw(Texture,
            new(vec2.X, vec0.Y),
            Vector2.Zero,
            Vector2.One,
            0,
            color,
            new(Borders.Right, 0),
            new(Texture.Width, Borders.Top));

        renderer.Draw(Texture,
            new(vec0.X, vec2.Y),
            Vector2.Zero,
            Vector2.One,
            0,
            color,
            new(0, Borders.Bottom),
            new(Borders.Left, Texture.Height));

        renderer.Draw(Texture, vec2, Vector2.Zero, Vector2.One, 0, color, new(Borders.Right, Borders.Bottom), Texture.Size);
    }

    public void Dispose() => Texture.Dispose();
}