namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using BrewLib.Graphics.Textures;
using BrewLib.Util;
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
        var fx2 = texture1 - texture0;
        var transform = Matrix3x2.CreateTranslation(-origin) * Matrix3x2.CreateScale(Vector2.Abs(scale)) *
            MathUtil.CreateRotationMatrixFast(rotation) * Matrix3x2.CreateTranslation(xy);

        var corner0 = Vector2.Transform(Vector2.Zero, transform);
        var corner1 = Vector2.Transform(new(0, fx2.Y), transform);
        var corner2 = Vector2.Transform(fx2, transform);
        var corner3 = corner2 - corner1 + corner0;

        var uvOrigin = texture.UvOrigin;
        var uvRatio = texture.UvRatio;

        var uv0 = Vector2.MultiplyAddEstimate(texture0, uvRatio, uvOrigin);
        var uv1 = Vector2.MultiplyAddEstimate(texture1, uvRatio, uvOrigin);

        Vector2 u0u1 = scale.X > 0 ? new(uv0.X, uv1.X) : new(uv1.X, uv0.X);
        Vector2 v0v1 = scale.Y > 0 ? new(uv0.Y, uv1.Y) : new(uv1.Y, uv0.Y);

        var rgba = color.ToPixel<Rgba32>();
        QuadPrimitive primitive = new()
        {
            vec1 = corner0,
            vec2 = corner1,
            vec3 = corner2,
            vec4 = corner3,
            u1 = (Half)u0u1.X,
            v1 = (Half)v0v1.X,
            u2 = (Half)u0u1.X,
            v2 = (Half)v0v1.Y,
            u3 = (Half)u0u1.Y,
            v3 = (Half)v0v1.Y,
            u4 = (Half)u0u1.Y,
            v4 = (Half)v0v1.X,
            color1 = rgba,
            color2 = rgba,
            color3 = rgba,
            color4 = rgba
        };

        renderer.Draw(ref primitive, texture);
    }
}