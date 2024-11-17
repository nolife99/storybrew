namespace BrewLib.Graphics.Renderers;

using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;

public static class LineRendererExtensions
{
    public static void DrawSquare(this LineRenderer line, Vector3 from, Vector3 to, Rgba32 color)
    {
        Vector3 topRight = new(to.X, from.Y, from.Z);
        Vector3 bottomLeft = new(from.X, to.Y, from.Z);

        line.Draw(from, topRight, color);
        line.Draw(topRight, to, color);
        line.Draw(to, bottomLeft, color);
        line.Draw(bottomLeft, from, color);
    }
}