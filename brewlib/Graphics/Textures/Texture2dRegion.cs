namespace BrewLib.Graphics.Textures;

using System;
using System.Numerics;
using SixLabors.ImageSharp;

public class Texture2dRegion : IDisposable
{
    public readonly Texture2d BindableTexture;
    readonly Rectangle bounds;

    public readonly Vector2 Size, UvOrigin, UvRatio;

    protected Texture2dRegion(Texture2d texture, Rectangle bounds)
    {
        texture ??= this as Texture2d;
        BindableTexture = texture;

        this.bounds = bounds;

        Size = new(bounds.Width, bounds.Height);
        UvOrigin = new Vector2(bounds.X, bounds.Y) / new Vector2(BindableTexture.Width, BindableTexture.Height);
        UvRatio = Vector2.One / new Vector2(BindableTexture.Width, BindableTexture.Height);
    }

    public int X => bounds.X;
    public int Y => bounds.Y;
    public int Width => bounds.Width;
    public int Height => bounds.Height;

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
        if (!disposed) disposed = true;
    }

    #endregion
}