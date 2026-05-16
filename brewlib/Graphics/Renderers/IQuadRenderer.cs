namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Textures;
using SixLabors.ImageSharp.PixelFormats;

public interface IQuadRenderer : IPrimitiveRenderer
{
    internal void Draw(scoped ref readonly QuadPrimitive quad, ITextureRegion texture);
}

[StructLayout(LayoutKind.Sequential)]
struct QuadPrimitive
{
    public Vector2 vec1;
    public Half u1, v1;
    public Rgba32 color1;

    public Vector2 vec2;
    public Half u2, v2;
    public Rgba32 color2;

    public Vector2 vec3;
    public Half u3, v3;
    public Rgba32 color3;

    public Vector2 vec4;
    public Half u4, v4;
    public Rgba32 color4;
}
