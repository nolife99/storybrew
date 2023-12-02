using BrewLib.Graphics.Cameras;
using System.Numerics;
using System;
using System.Drawing;

namespace BrewLib.Graphics.Drawables;

public interface Drawable : IDisposable
{
    Vector2 MinSize { get; }
    Vector2 PreferredSize { get; }

    void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity = 1);
}