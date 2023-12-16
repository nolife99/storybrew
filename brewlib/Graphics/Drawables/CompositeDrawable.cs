using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using BrewLib.Graphics.Cameras;
using BrewLib.Util;

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

            for (var i = 0; i < Drawables.Count; ++i) 
            {
                var minSize = Drawables[i].MinSize;
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

            for (var i = 0; i < Drawables.Count; ++i)
            {
                var preferredSize = Drawables[i].PreferredSize;
                maxWidth = Math.Min(maxWidth, preferredSize.X);
                maxHeight = Math.Min(maxHeight, preferredSize.Y);
            }
            return new(maxWidth, maxHeight);
        }
    }
    public void Draw(DrawContext drawContext, Camera camera, RectangleF bounds, float opacity)
        => Drawables.ForEachUnsafe(drawable => drawable.Draw(drawContext, camera, bounds, opacity));

    #region IDisposable Support

    protected virtual void Dispose(bool disposing) { }
    public void Dispose() => Dispose(true);

    #endregion
}