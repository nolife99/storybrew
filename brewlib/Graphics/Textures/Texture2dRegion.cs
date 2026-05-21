namespace BrewLib.Graphics.Textures;

using System;
using System.Numerics;
using SixLabors.ImageSharp;

public class Texture2dRegion : ITextureRegion
{
    readonly Rectangle bounds;

    public readonly ITexture BindableTexture;

    public ITexture Texture => BindableTexture;
    public Rectangle Bounds => bounds;
    public Size Size => bounds.Size;
    public Vector2 UvOrigin { get; }
    public Vector2 UvRatio { get; }

    protected Texture2dRegion(ITexture texture, Rectangle bounds)
    {
        texture ??= this as ITexture;
        BindableTexture = texture!;

        this.bounds = bounds;

        UvOrigin = new Vector2(bounds.X, bounds.Y) /
            new Vector2(BindableTexture.Size.Width, BindableTexture.Size.Height);
        UvRatio = Vector2.One / new Vector2(BindableTexture.Size.Width, BindableTexture.Size.Height);
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
