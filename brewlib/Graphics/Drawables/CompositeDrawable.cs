namespace BrewLib.Graphics.Drawables;

using System.Collections.Generic;
using System.Numerics;
using Cameras;
using SixLabors.ImageSharp;

public sealed class CompositeDrawable : Drawable
{
    public List<Drawable> Drawables { get; } = [];

    public Vector2 MinSize
    {
        get
        {
            var minWidth = 0f;
            var minHeight = 0f;

            foreach (var drawable in Drawables)
            {
                var minSize = drawable.MinSize;
                minWidth = float.Min(minWidth, minSize.X);
                minHeight = float.Min(minWidth, minSize.Y);
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
                maxWidth = float.Min(maxWidth, preferredSize.X);
                maxHeight = float.Min(maxHeight, preferredSize.Y);
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
        foreach (var drawable in Drawables) drawable.Dispose();
    }

    #endregion
}