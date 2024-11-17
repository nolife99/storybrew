namespace BrewLib.Graphics.Textures;

using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public sealed class TextureAtlas2d(int width,
    int height,
    string description,
    TextureOptions textureOptions = null,
    int padding = 0) : IDisposable
{
    readonly Texture2d texture = Texture2d.Create(default, description, width, height, textureOptions);
    int currentX, currentY, nextY;

    public Texture2dRegion AddRegion(Image<Rgba32> bitmap)
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
        Texture2dRegion region = new(texture, new(currentX, currentY, width, height));

        currentX += width + padding;
        nextY = Math.Max(nextY, currentY + height + padding);

        return region;
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;
        texture.Dispose();
        disposed = true;
    }

    #endregion
}