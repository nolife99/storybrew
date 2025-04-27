namespace BrewLib.Graphics.Renderers;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using Textures;

public static class QuadRendererExtensions
{
    public static void Draw(this IQuadRenderer renderer,
        Texture2dRegion texture,
        Vector2 xy,
        Vector2 origin,
        Vector2 scale,
        float rotation,
        Rgba32 color,
        Vector2 texture0,
        Vector2 texture1)
    {
        var flip = Vector2.CopySign(Vector2.One, scale);
        var fx = -origin * scale * flip;
        var fx2 = (texture1 - texture0 - origin) * scale * flip;

        ref var cornersRef = ref MemoryMarshal.GetReference(stackalloc Vector2[4]);
        if (rotation != 0)
        {
            var rotationMatrix = Matrix3x2.CreateRotation(rotation);

            cornersRef = Vector2.Transform(fx, rotationMatrix);
            ref var temp1 = ref Unsafe.Add(ref cornersRef, 1);
            ref var temp2 = ref Unsafe.Add(ref cornersRef, 2);

            temp1 = Vector2.Transform(fx with { Y = fx2.Y }, rotationMatrix);
            temp2 = Vector2.Transform(fx2, rotationMatrix);
            Unsafe.Add(ref cornersRef, 3) = temp2 - temp1 + cornersRef;
        }
        else
        {
            cornersRef = fx;
            Unsafe.Add(ref cornersRef, 1) = fx with { Y = fx2.Y };
            Unsafe.Add(ref cornersRef, 2) = fx2;
            Unsafe.Add(ref cornersRef, 3) = fx2 with { Y = fx.Y };
        }

        var textureUvOrigin = texture.UvOrigin;
        var textureUvRatio = texture.UvRatio;

        var textureU0V0 = Vector2.FusedMultiplyAdd(texture0, textureUvRatio, textureUvOrigin);
        var textureU1V1 = Vector2.FusedMultiplyAdd(texture1, textureUvRatio, textureUvOrigin);

        var textureU0U1 = flip.X > 0 ? textureU0V0 with { Y = textureU1V1.X } : textureU1V1 with { Y = textureU0V0.X };
        var textureV0V1 = flip.Y > 0 ? textureU1V1 with { X = textureU0V0.Y } : textureU0V0 with { X = textureU1V1.Y };

        QuadPrimitive primitive = new()
        {
            vec1 = cornersRef + xy,
            vec2 = Unsafe.Add(ref cornersRef, 1) + xy,
            vec3 = Unsafe.Add(ref cornersRef, 2) + xy,
            vec4 = Unsafe.Add(ref cornersRef, 3) + xy,
            u1 = textureU0U1.X,
            u2 = textureU0U1.X,
            u3 = textureU0U1.Y,
            u4 = textureU0U1.Y,
            v1 = textureV0V1.X,
            v2 = textureV0V1.Y,
            v3 = textureV0V1.Y,
            v4 = textureV0V1.X,
            color1 = color,
            color2 = color,
            color3 = color,
            color4 = color
        };

        renderer.Draw(ref primitive, texture);
    }
}