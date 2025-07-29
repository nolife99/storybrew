namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Textures;
using SixLabors.ImageSharp.PixelFormats;

public interface IQuadRenderer : IRenderer, IDisposable
{
    internal void Draw(ref readonly QuadPrimitive quad, Texture2dRegion texture);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
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