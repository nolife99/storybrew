using BrewLib.Graphics.Cameras;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Drawing;

namespace BrewLib.Graphics.Drawables;

public class CompositeDrawable : Drawable
{
    public readonly List<Drawable> Drawables = [];

    public Vector2 MinSize
    {
        get
        {
            var minWidth = 0f;
            var minHeight = 0f;

            Drawables.ForEach(drawable =>
            {
                var minSize = drawable.MinSize;
                minWidth = Math.Min(minWidth, minSize.X);
                minHeight = Math.Min(minWidth, minSize.Y);
            });
            return new(minWidth, minHeight);
        }
    }
    public Vector2 PreferredSize
    {
        get
        {
            var maxWidth = 0f;
            var maxHeight = 0f;

            Drawables.ForEach(drawable =>
            {
                var preferredSize = drawable.PreferredSize;
                maxWidth = Math.Min(maxWidth, preferredSize.X);
                maxHeight = Math.Min(maxHeight, preferredSize.Y);
            });
            return new(maxWidth, maxHeight);
        }
    }
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity)
        => Drawables.ForEach(drawable => drawable.Draw(drawContext, camera, bounds, opacity));

    #region IDisposable Support

    protected virtual void Dispose(bool disposing) { }
    public void Dispose() => Dispose(true);

    #endregion
}