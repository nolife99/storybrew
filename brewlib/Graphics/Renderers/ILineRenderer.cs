namespace BrewLib.Graphics.Renderers;

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public interface ILineRenderer : IPrimitiveRenderer
{
    internal void Draw(scoped ref readonly Vector3 start,
        scoped ref readonly Vector3 end,
        scoped ref readonly Color color);
}

[StructLayout(LayoutKind.Sequential)]
struct LinePrimitive
{
    public Vector3 from;
    public Rgba32 color1;
    public Vector3 to;
    public Rgba32 color2;
}
