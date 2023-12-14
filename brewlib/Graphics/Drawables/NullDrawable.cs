using System.Drawing;
using System.Numerics;
using BrewLib.Graphics.Cameras;

namespace BrewLib.Graphics.Drawables;

public class NullDrawable : Drawable
{
    public static readonly Drawable Instance = new NullDrawable();

    public Vector2 MinSize => Vector2.Zero;
    public Vector2 PreferredSize => Vector2.Zero;

    NullDrawable() { }
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity = 1) { }
    public void Dispose() { }
}