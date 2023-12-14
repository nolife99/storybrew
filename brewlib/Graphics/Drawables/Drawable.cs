using System;
using System.Drawing;
using System.Numerics;
using BrewLib.Graphics.Cameras;

namespace BrewLib.Graphics.Drawables;

public interface Drawable : IDisposable
{
    Vector2 MinSize { get; }
    Vector2 PreferredSize { get; }

    void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity = 1);
}