namespace BrewLib.Graphics.Textures;

using System;
using System.Drawing;

public sealed class TextureAtlas2d(
    int width, int height, string description, TextureOptions textureOptions = null, int padding = 0) : IDisposable
{
    int currentX, currentY, nextY;
    Texture2d texture = Texture2d.Create(default, description, width, height, textureOptions);

    public Texture2dRegion AddRegion(Bitmap bitmap, string description)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;

        if (currentY + height > texture.Height) return null;
        if (currentX + width > texture.Width)
        {
            if (nextY + height > texture.Height) return null;
            currentX = 0;
            currentY = nextY;
        }

        texture.Update(bitmap, currentX, currentY, textureOptions);
        Texture2dRegion region = new(texture, new(currentX, currentY, width, height), description);

        currentX += width + padding;
        nextY = Math.Max(nextY, currentY + height + padding);

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

            GC.SuppressFinalize(this);
            disposed = true;
        }
    }

#endregion
}