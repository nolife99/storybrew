namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using BrewLib.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public static class QuadRendererExtensions
{
    public static void Draw(this IQuadRenderer renderer,
        Texture2dRegion texture,
        Vector2 xy,
        Vector2 origin,
        Vector2 scale,
        float rotation,
        Color color,
        Vector2 texture0,
        Vector2 texture1)
    {
        var flip = Vector2.CopySign(Vector2.One, scale);
        var fx = -origin * scale * flip;
        var fx2 = (texture1 - texture0 - origin) * scale * flip;

        var transform = Matrix3x2.CreateTranslation(xy) * Matrix3x2.CreateRotation(rotation);
        var corner0 = Vector2.Transform(fx, transform);
        var corner1 = Vector2.Transform(new(fx.X, fx2.Y), transform);
        var corner2 = Vector2.Transform(fx2, transform);
        var corner3 = corner2 - corner1 + corner0;

        var uvOrigin = texture.UvOrigin;
        var uvRatio = texture.UvRatio;

        var textureU0V0 = Vector2.MultiplyAddEstimate(texture0, uvRatio, uvOrigin);
        var textureU1V1 = Vector2.MultiplyAddEstimate(texture1, uvRatio, uvOrigin);

        Vector2 textureU0U1 = flip.X > 0 ? new(textureU0V0.X, textureU1V1.X) : new(textureU1V1.X, textureU0V0.X);
        Vector2 textureV0V1 = flip.Y > 0 ? new(textureU0V0.Y, textureU1V1.Y) : new(textureU1V1.Y, textureU0V0.Y);

        var rgba = color.ToPixel<Rgba32>();
        QuadPrimitive primitive = new()
        {
            vec1 = corner0,
            vec2 = corner1,
            vec3 = corner2,
            vec4 = corner3,
            u1 = (Half)textureU0U1.X,
            v1 = (Half)textureV0V1.X,
            u2 = (Half)textureU0U1.X,
            v2 = (Half)textureV0V1.Y,
            u3 = (Half)textureU0U1.Y,
            v3 = (Half)textureV0V1.Y,
            u4 = (Half)textureU0U1.Y,
            v4 = (Half)textureV0V1.X,
            color1 = rgba,
            color2 = rgba,
            color3 = rgba,
            color4 = rgba
        };

        renderer.Draw(ref primitive, texture);
    }
}