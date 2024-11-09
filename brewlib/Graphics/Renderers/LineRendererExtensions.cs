namespace BrewLib.Graphics.Renderers;

using System.Drawing;
using System.Numerics;

public static class LineRendererExtensions
{
    public static void Draw(this LineRenderer line, Vector2 from, Vector2 to, Color color)
        => line.Draw(new(from.X, from.Y, 0), new(to.X, to.Y, 0), color);

    public static void Draw(this LineRenderer line, Vector2 from, Vector2 to, Color startColor, Color endColor)
        => line.Draw(new(from.X, from.Y, 0), new(to.X, to.Y, 0), startColor, endColor);

    public static void DrawSquare(this LineRenderer line, Vector3 from, Vector3 to, Color color)
    {
        Vector3 topRight = new(to.X, from.Y, from.Z);
        Vector3 bottomLeft = new(from.X, to.Y, from.Z);

        line.Draw(from, topRight, color);
        line.Draw(topRight, to, color);
        line.Draw(to, bottomLeft, color);
        line.Draw(bottomLeft, from, color);
    }
}