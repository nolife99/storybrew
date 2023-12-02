using System.Drawing;
using System;
using Bitmap = System.Drawing.Bitmap;

namespace BrewLib.Graphics.Textures;

public sealed class TextureAtlas2d(int width, int height, string description, TextureOptions textureOptions = null, int padding = 0) : IDisposable
{
    Texture2d texture = Texture2d.Create(Color.FromArgb(0, 0, 0, 0), description, width, height, textureOptions);
    int currentX, currentY, nextY;

    public float FillRatio => (texture.Width * currentY + currentX * (nextY - currentY)) / (texture.Width * texture.Height);

    public Texture2dRegion AddRegion(Bitmap bitmap, string description)
    {
        if (currentY + bitmap.Height > texture.Height) return null;
        if (currentX + bitmap.Width > texture.Width)
        {
            if (nextY + bitmap.Height > texture.Height) return null;
            currentX = 0;
            currentY = nextY;
        }

        texture.Update(bitmap, currentX, currentY, textureOptions);
        Texture2dRegion region = new(texture, new(currentX, currentY, bitmap.Width, bitmap.Height), description);

        currentX += bitmap.Width + padding;
        nextY = Math.Max(nextY, currentY + bitmap.Height + padding);

        return region;
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            texture.Dispose();
            texture = null;
            disposed = true;
        }
    }

    #endregion
}