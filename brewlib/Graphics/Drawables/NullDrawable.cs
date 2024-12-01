namespace BrewLib.Graphics.Drawables;

using System.Numerics;
using Cameras;
using SixLabors.ImageSharp;

public sealed class NullDrawable : Drawable
{
    public static readonly Drawable Instance = new NullDrawable();

    NullDrawable() { }

    public Vector2 MinSize => Vector2.Zero;
    public Vector2 PreferredSize => Vector2.Zero;
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity = 1) { }
    public void Dispose() { }
}