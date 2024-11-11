namespace BrewLib.Graphics.Textures;

using System;
using System.Drawing;
using System.Numerics;

public class Texture2dRegion : Texture
{
    readonly RectangleF bounds;
    readonly string description;

    public Texture2dRegion(Texture2d texture, RectangleF bounds, string description)
    {
        BindableTexture = texture ?? this as Texture2d;
        this.bounds = bounds;
        this.description = description;
    }

    public Vector2 Size => new(bounds.Width, bounds.Height);

    public float Width => bounds.Width;
    public float Height => bounds.Height;

    public RectangleF UvBounds => RectangleF.FromLTRB(bounds.Left / BindableTexture.Width, bounds.Top / BindableTexture.Height,
        bounds.Right / BindableTexture.Width, bounds.Bottom / BindableTexture.Height);

    public Vector2 UvRatio => new(1 / BindableTexture.Width, 1 / BindableTexture.Height);
    public string Description => BindableTexture != this ? $"{description} (from {BindableTexture.Description})" : description;
    public Texture2d BindableTexture { get; private set; }

    public virtual void Update(Bitmap bitmap, int x, int y, TextureOptions textureOptions)
    {
        if (BindableTexture is null) throw new InvalidOperationException();
        if (x < 0 || y < 0) throw new ArgumentOutOfRangeException();

        var updateX = bounds.Left + x;
        var updateY = bounds.Top + y;

        if (updateX + bitmap.Width > bounds.Right || updateY + bitmap.Height > bounds.Bottom)
            throw new ArgumentOutOfRangeException();

        BindableTexture.Update(bitmap, (int)updateX, (int)updateY, textureOptions);
    }

    #region IDisposable Support

    ~Texture2dRegion() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (disposed || !disposing) return;
        BindableTexture = null;
        disposed = true;
    }

    #endregion
}