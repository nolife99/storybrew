namespace BrewLib.Graphics.Renderers;

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

public static class LineRendererExtensions
{
    public static void DrawSquare(this LineRenderer line, Vector3 from, Vector3 to, Rgba32 color)
    {
        Vector3 topRight = new(to.X, from.Y, from.Z);
        Vector3 bottomLeft = new(from.X, to.Y, from.Z);

        line.Draw(ref from, ref topRight, ref color);
        line.Draw(ref topRight, ref to, ref color);
        line.Draw(ref to, ref bottomLeft, ref color);
        line.Draw(ref bottomLeft, ref from, ref color);
    }
}