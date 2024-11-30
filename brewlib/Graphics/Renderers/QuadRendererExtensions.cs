namespace BrewLib.Graphics.Renderers;

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Textures;

public static class QuadRendererExtensions
{
    public static void Draw(this QuadRenderer renderer,
        Texture2dRegion texture,
        float x,
        float y,
        float originX,
        float originY,
        float scaleX,
        float scaleY,
        float rotation,
        Rgba32 color,
        float textureX0,
        float textureY0,
        float textureX1,
        float textureY1)
    {
        float width = textureX1 - textureX0, height = textureY1 - textureY0, fx = -originX, fy = -originY, fx2 = width - originX,
            fy2 = height - originY;

        bool flipX = false, flipY = false;

        if (scaleX != 1 || scaleY != 1)
        {
            flipX = scaleX < 0;
            flipY = scaleY < 0;

            float absScaleX = flipX ? -scaleX : scaleX, absScaleY = flipY ? -scaleY : scaleY;
            fx *= absScaleX;
            fy *= absScaleY;
            fx2 *= absScaleX;
            fy2 *= absScaleY;
        }

        float x1, y1, x2, y2, x3, y3, x4, y4;
        if (rotation != 0)
        {
            var (sin, cos) = float.SinCos(rotation);
            x1 = cos * fx - sin * fy;
            y1 = sin * fx + cos * fy;
            x2 = cos * fx - sin * fy2;
            y2 = sin * fx + cos * fy2;
            x3 = cos * fx2 - sin * fy2;
            y3 = sin * fx2 + cos * fy2;
            x4 = x1 + x3 - x2;
            y4 = y3 - y2 + y1;
        }
        else
        {
            x1 = fx;
            y1 = fy;
            x2 = fx;
            y2 = fy2;
            x3 = fx2;
            y3 = fy2;
            x4 = fx2;
            y4 = fy;
        }

        Vector2 textureUvOrigin = texture.UvOrigin, textureUvRatio = texture.UvRatio;
        float textureU0 = textureUvOrigin.X + textureX0 * textureUvRatio.X,
            textureV0 = textureUvOrigin.Y + textureY0 * textureUvRatio.Y,
            textureU1 = textureUvOrigin.X + textureX1 * textureUvRatio.X,
            textureV1 = textureUvOrigin.Y + textureY1 * textureUvRatio.Y, u0, v0, u1, v1;

        if (flipX)
        {
            u0 = textureU1;
            u1 = textureU0;
        }
        else
        {
            u0 = textureU0;
            u1 = textureU1;
        }

        if (flipY)
        {
            v0 = textureV1;
            v1 = textureV0;
        }
        else
        {
            v0 = textureV0;
            v1 = textureV1;
        }

        QuadPrimitive primitive = new()
        {
            x1 = x1 + x,
            y1 = y1 + y,
            x2 = x2 + x,
            y2 = y2 + y,
            x3 = x3 + x,
            y3 = y3 + y,
            x4 = x4 + x,
            y4 = y4 + y,
            u1 = u0,
            u2 = u0,
            u3 = u1,
            u4 = u1,
            v1 = v0,
            v2 = v1,
            v3 = v1,
            v4 = v0,
            color1 = color,
            color2 = color,
            color3 = color,
            color4 = color
        };

        renderer.Draw(ref primitive, texture);
    }
}