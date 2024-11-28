namespace BrewLib.Graphics.Renderers;

using System;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using Textures;

public interface QuadRenderer : Renderer, IDisposable
{
    void Draw(ref readonly QuadPrimitive quad, Texture2dRegion texture);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public record struct QuadPrimitive
{
    public float x1, y1, u1, v1;
    public Rgba32 color1;
    public float x2, y2, u2, v2;
    public Rgba32 color2;
    public float x3, y3, u3, v3;
    public Rgba32 color3;
    public float x4, y4, u4, v4;
    public Rgba32 color4;
}