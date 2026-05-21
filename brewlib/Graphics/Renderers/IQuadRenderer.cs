namespace BrewLib.Graphics.Renderers;

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using Textures;

public interface IQuadRenderer : IPrimitiveRenderer
{
    internal void Draw(scoped ref readonly QuadPrimitive quad, ITextureRegion texture);
}

[StructLayout(LayoutKind.Sequential)]
struct QuadPrimitive
{
    public Vector2 vec1;
    public float u1, v1;
    public Rgba32 color1;

    public Vector2 vec2;
    public float u2, v2;
    public Rgba32 color2;

    public Vector2 vec3;
    public float u3, v3;
    public Rgba32 color3;

    public Vector2 vec4;
    public float u4, v4;
    public Rgba32 color4;
}
