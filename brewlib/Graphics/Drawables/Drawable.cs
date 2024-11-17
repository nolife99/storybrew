namespace BrewLib.Graphics.Drawables;

using System;
using System.Numerics;
using Cameras;
using SixLabors.ImageSharp;

public interface Drawable : IDisposable
{
    Vector2 MinSize { get; }
    Vector2 PreferredSize { get; }

    void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity = 1);
}