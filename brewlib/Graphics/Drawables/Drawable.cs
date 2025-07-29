namespace BrewLib.Graphics.Drawables;

using System;
using System.Numerics;
using BrewLib.Graphics.Cameras;
using SixLabors.ImageSharp;

public interface Drawable : IDisposable
{
    Vector2 MinSize { get; }
    Vector2 PreferredSize { get; }

    void Draw(DrawContext drawContext, ICamera camera, RectangleF bounds, float opacity = 1);
}