using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using BrewLib.Graphics.Cameras;

namespace BrewLib.Graphics.Drawables;

public class CompositeDrawable : Drawable
{
    public List<Drawable> Drawables { get; private set; } = [];

    public Vector2 MinSize
    {
        get
        {
            var minWidth = 0f;
            var minHeight = 0f;

            foreach (var drawable in Drawables)
            {
                var minSize = drawable.MinSize;
                minWidth = Math.Min(minWidth, minSize.X);
                minHeight = Math.Min(minWidth, minSize.Y);
            }
            return new(minWidth, minHeight);
        }
    }
    public Vector2 PreferredSize
    {
        get
        {
            var maxWidth = 0f;
            var maxHeight = 0f;

            foreach (var drawable in Drawables)
            {
                var preferredSize = drawable.PreferredSize;
                maxWidth = Math.Min(maxWidth, preferredSize.X);
                maxHeight = Math.Min(maxHeight, preferredSize.Y);
            }
            return new(maxWidth, maxHeight);
        }
    }
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity)
    {
        foreach (var drawable in Drawables) drawable.Draw(drawContext, camera, bounds, opacity);
    }

    #region IDisposable Support

    public void Dispose()
    {
        Drawables.Clear();
        Drawables = null;
        GC.SuppressFinalize(this);
    }

    #endregion
}