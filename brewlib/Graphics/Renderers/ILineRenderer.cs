namespace BrewLib.Graphics.Renderers;

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public interface ILineRenderer : IPrimitiveRenderer
{
    internal void Draw(ref readonly Vector3 start, ref readonly Vector3 end, ref readonly Color color);
}

[StructLayout(LayoutKind.Sequential)]
struct LinePrimitive
{
    public Vector3 from;
    public Rgba32 color1;
    public Vector3 to;
    public Rgba32 color2;
}
