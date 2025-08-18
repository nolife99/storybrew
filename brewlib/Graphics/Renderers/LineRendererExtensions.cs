namespace BrewLib.Graphics.Renderers;

using System.Numerics;
using SixLabors.ImageSharp;

public static class LineRendererExtensions
{
    public static void DrawSquare(this ILineRenderer line, Vector3 from, Vector3 to, Color color)
    {
        Vector3 topRight = new(to.X, from.Y, from.Z);
        Vector3 bottomLeft = new(from.X, to.Y, from.Z);

        line.Draw(in from, in topRight, in color);
        line.Draw(in topRight, in to, in color);
        line.Draw(in to, in bottomLeft, in color);
        line.Draw(in bottomLeft, in from, in color);
    }
}