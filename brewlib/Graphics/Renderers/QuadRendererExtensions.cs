namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
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
        scale = Vector2.Abs(scale);
        Vector2 flip = new(scale.X < 0 ? -1 : 1, scale.Y < 0 ? -1 : 1);
        var size = texture1 - texture0;

        var fx = -origin * scale * flip;
        var fx2 = (size - origin) * scale * flip;

        Span<Vector2> corners = stackalloc Vector2[4];
        if (rotation != 0)
        {
            var rotationMatrix = Matrix3x2.CreateRotation(rotation);

            corners[0] = Vector2.Transform(fx, rotationMatrix);
            corners[1] = Vector2.Transform(fx with { Y = fx2.Y }, rotationMatrix);
            corners[2] = Vector2.Transform(fx2, rotationMatrix);
            corners[3] = corners[0] + (corners[2] - corners[1]);
        }
        else
        {
            corners[0] = fx;
            corners[1] = fx with { Y = fx2.Y };
            corners[2] = fx2;
            corners[3] = fx2 with { Y = fx.Y };
        }

        for (var i = 0; i < 4; i++) corners[i] += xy;

        var textureUvOrigin = texture.UvOrigin;
        var textureUvRatio = texture.UvRatio;

        var textureU0V0 = Vector2.FusedMultiplyAdd(texture0, textureUvRatio, textureUvOrigin);
        var textureU1V1 = Vector2.FusedMultiplyAdd(texture1, textureUvRatio, textureUvOrigin);

        var textureU0U1 = flip.X > 0 ? textureU0V0 with { Y = textureU1V1.X } : textureU1V1 with { Y = textureU0V0.X };
        var textureV0V1 = flip.Y > 0 ? textureU1V1 with { X = textureU0V0.Y } : textureU0V0 with { X = textureU1V1.Y };

        QuadPrimitive primitive = new()
        {
            vec1 = corners[0],
            vec2 = corners[1],
            vec3 = corners[2],
            vec4 = corners[3],
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