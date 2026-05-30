namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Textures;

public static class QuadRendererExtensions
{
    public static void Draw(this IQuadRenderer renderer,
        ITextureRegion texture,
        Vector2 xy,
        Vector2 origin,
        Vector2 scale,
        float rotation,
        Color color,
        Vector2 texture0,
        Vector2 texture1)
    {
        var uvOrigin = texture.UvOrigin;
        var uvRatio = texture.UvRatio;
        Vector2 uv0, uv1;

        if (texture is ITrimmedTextureRegion trimmed)
        {
            // The backing texture stores only the opaque content sub-rect of a larger logical image (the transparent
            // margin was trimmed to save VRAM). Draw a quad covering just that content, repositioned so it lands where
            // it sat in the full image, sampling the (smaller, possibly block-padded) backing texture.
            //
            // Assumes a full-sprite draw (texture0 = 0, texture1 = Width/Height), which is how storyboard sprites are
            // drawn; a trimmed region isn't meant to be drawn with a sub-rect.
            //
            // Mirroring (negative scale) is applied via the UV swap below with the geometry left unmirrored, so the
            // reposition offset must mirror with the sign of scale — otherwise flipped content lands on the wrong side.
            var content = trimmed.ContentBounds;
            var offsetX = scale.X >= 0 ? content.X : texture.Width - content.X - content.Width;
            var offsetY = scale.Y >= 0 ? content.Y : texture.Height - content.Y - content.Height;
            origin -= new Vector2(offsetX, offsetY);

            texture0 = Vector2.Zero;
            texture1 = new Vector2(content.Width, content.Height);
            uv0 = uvOrigin; // content begins at (0,0) of the backing texture
            uv1 = Vector2.MultiplyAddEstimate(texture1, uvRatio, uvOrigin);
        }
        else
        {
            uv0 = Vector2.MultiplyAddEstimate(texture0, uvRatio, uvOrigin);
            uv1 = Vector2.MultiplyAddEstimate(texture1, uvRatio, uvOrigin);
        }

        // Build the affine transform once and feed it straight to the renderer
        // — the unit-quad shader reads (column0, column1, translation) directly.
        var fx2 = texture1 - texture0;
        var transform = Matrix3x2.CreateTranslation(-origin) * Matrix3x2.CreateScale(Vector2.Abs(scale)) *
            Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(xy);

        // Scale the transform's basis vectors by the sprite size so vertex (1,0)/(0,1)
        // map to the original sprite's right/bottom edges.
        var instanceTransform = new Matrix3x2(
            transform.M11 * fx2.X,
            transform.M12 * fx2.X,
            transform.M21 * fx2.Y,
            transform.M22 * fx2.Y,
            transform.M31,
            transform.M32);

        var u0u1 = scale.X > 0 ? new(uv0.X, uv1.X) : new Vector2(uv1.X, uv0.X);
        var v0v1 = scale.Y > 0 ? new(uv0.Y, uv1.Y) : new Vector2(uv1.Y, uv0.Y);

        QuadInstance instance = new()
        {
            Transform = instanceTransform,
            U = (Half)u0u1.X,
            V = (Half)v0v1.X,
            UAxis = (Half)(u0u1.Y - u0u1.X),
            VAxis = (Half)(v0v1.Y - v0v1.X),
            Color = color.ToPixel<Rgba32>()
        };

        renderer.Draw(in instance, texture);
    }
}
