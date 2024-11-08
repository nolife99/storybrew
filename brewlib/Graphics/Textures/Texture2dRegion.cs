namespace BrewLib.Graphics.Textures;

using System;
using System.Drawing;
using System.Numerics;

public class Texture2dRegion : Texture
{
    readonly string description;

    readonly RectangleF bounds;

    Texture2d texture;

    public Texture2dRegion(Texture2d texture, RectangleF bounds, string description)
    {
        this.texture = texture ?? this as Texture2d;
        this.bounds = bounds;
        this.description = description;
    }

    public RectangleF Bounds => bounds;
    public float X => bounds.Left;
    public float Y => bounds.Top;

    public float Width => bounds.Width;
    public float Height => bounds.Height;
    public Vector2 Size => new(bounds.Width, bounds.Height);

    public RectangleF UvBounds
        => RectangleF.FromLTRB(bounds.Left / texture.Width, bounds.Top / texture.Height, bounds.Right / texture.Width,
            bounds.Bottom / texture.Height);

    public Vector2 UvRatio => new(1 / texture.Width, 1 / texture.Height);
    public string Description => texture != this ? $"{description} (from {texture.Description})" : description;
    public BindableTexture BindableTexture => texture;

    public virtual void Update(Bitmap bitmap, int x, int y, TextureOptions textureOptions)
    {
        if (texture is null) throw new InvalidOperationException();
        if (x < 0 || y < 0) throw new ArgumentOutOfRangeException();

        var updateX = bounds.Left + x;
        var updateY = bounds.Top + y;

        if (updateX + bitmap.Width > bounds.Right || updateY + bitmap.Height > bounds.Bottom)
            throw new ArgumentOutOfRangeException();

        texture.Update(bitmap, (int)updateX, (int)updateY, textureOptions);
    }

#region IDisposable Support

    bool disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed && disposing)
        {
            texture = null;
            disposed = true;
        }
    }

    ~Texture2dRegion() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

#endregion
}