namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Textures;

public interface IQuadRenderer : IPrimitiveRenderer
{
    internal void Draw(scoped ref readonly QuadInstance instance, ITextureRegion texture);
}

struct QuadInstance
{
    public Matrix3x2 Transform;
    public Half U, V, UAxis, VAxis;
    public Rgba32 Color;
}