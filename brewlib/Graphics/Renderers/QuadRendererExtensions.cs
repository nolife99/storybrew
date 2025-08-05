namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

        ref var corner0 = ref MemoryMarshal.GetReference(stackalloc Vector2[4]);
        if (rotation != 0)
        {
            var rotationMatrix = Matrix3x2.CreateRotation(rotation);

            corner0 = Vector2.Transform(fx, rotationMatrix);
            ref var corner1 = ref Unsafe.Add(ref corner0, 1);
            ref var corner2 = ref Unsafe.Add(ref corner0, 2);

            corner1 = Vector2.Transform(new(fx.X, fx2.Y), rotationMatrix);
            corner2 = Vector2.Transform(fx2, rotationMatrix);
            Unsafe.Add(ref corner0, 3) = corner2 - corner1 + corner0;
        }
        else
        {
            corner0 = fx;
            Unsafe.Add(ref corner0, 1) = new(fx.X, fx2.Y);
            Unsafe.Add(ref corner0, 2) = fx2;
            Unsafe.Add(ref corner0, 3) = new(fx2.X, fx.Y);
        }

        var textureUvOrigin = texture.UvOrigin;
        var textureUvRatio = texture.UvRatio;

        var textureU0V0 = Vector2.MultiplyAddEstimate(texture0, textureUvRatio, textureUvOrigin);
        var textureU1V1 = Vector2.MultiplyAddEstimate(texture1, textureUvRatio, textureUvOrigin);

        Vector2 textureU0U1 = flip.X > 0 ? new(textureU0V0.X, textureU1V1.X) : new(textureU1V1.X, textureU0V0.X);
        Vector2 textureV0V1 = flip.Y > 0 ? new(textureU0V0.Y, textureU1V1.Y) : new(textureU1V1.Y, textureU0V0.Y);

        var rgba = color.ToPixel<Rgba32>();
        QuadPrimitive primitive = new()
        {
            vec1 = corner0 + xy,
            vec2 = Unsafe.Add(ref corner0, 1) + xy,
            vec3 = Unsafe.Add(ref corner0, 2) + xy,
            vec4 = Unsafe.Add(ref corner0, 3) + xy,
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