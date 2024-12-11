namespace BrewLib.Graphics.Renderers;

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

public interface LineRenderer : Renderer, IDisposable
{
    void Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Rgba32 color);
}

[StructLayout(LayoutKind.Sequential)] public ref struct LinePrimitive
{
    public Vector3 from;
    public Rgba32 color1;
    public Vector3 to;
    public Rgba32 color2;
}