namespace BrewLib.Graphics.Textures;

using System;
using System.Numerics;
using SixLabors.ImageSharp;

public class Texture2dRegion : IDisposable
{
    readonly Rectangle bounds;

    public Texture2dRegion(Texture2d texture, Rectangle bounds)
    {
        BindableTexture = texture ?? this as Texture2d;
        this.bounds = bounds;
    }

    public Vector2 Size => new(bounds.Width, bounds.Height);

    public int Width => bounds.Width;
    public int Height => bounds.Height;

    public RectangleF UvBounds => RectangleF.FromLTRB((float)bounds.Left / BindableTexture.Width,
        (float)bounds.Top / BindableTexture.Height, (float)bounds.Right / BindableTexture.Width,
        (float)bounds.Bottom / BindableTexture.Height);

    public Vector2 UvRatio => new(1f / BindableTexture.Width, 1f / BindableTexture.Height);
    public Texture2d BindableTexture { get; }

    #region IDisposable Support

    ~Texture2dRegion() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed && disposing) disposed = true;
    }

    #endregion
}