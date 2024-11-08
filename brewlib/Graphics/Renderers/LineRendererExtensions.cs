using static System.MathF;

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

    public static void DrawCone(this LineRenderer line, Vector3 center, float arc, float orientation, float innerRadius,
        float radius, Color color, float precision = 1)
    {
        var fromAngle = orientation - arc * .5f;
        var toAngle = orientation + arc * .5f;

        line.Draw(center + new Vector3(Cos(fromAngle) * innerRadius, Sin(fromAngle) * innerRadius, 0),
            center + new Vector3(Cos(fromAngle) * radius, Sin(fromAngle) * radius, 0), color);
        line.Draw(center + new Vector3(Cos(toAngle) * innerRadius, Sin(toAngle) * innerRadius, 0),
            center + new Vector3(Cos(toAngle) * radius, Sin(toAngle) * radius, 0), color);

        var minLineCount = Max(Round(16 * (arc / Tau)), 2);

        var outerCircumference = arc * radius;
        var outerLineCount = Max(minLineCount, Round(outerCircumference * precision));

        var angleStep = arc / outerLineCount;
        var previousPosition = center + new Vector3(Cos(fromAngle) * radius, Sin(fromAngle) * radius, 0);

        for (var i = 1; i <= outerLineCount; ++i)
        {
            var angle = fromAngle + angleStep * i;
            Vector3 position = new(center.X + Cos(angle) * radius, center.Y + Sin(angle) * radius, center.Z);
            line.Draw(previousPosition, position, color);
            previousPosition = position;
        }

        if (innerRadius > 0)
        {
            var innerCircumference = arc * innerRadius;
            var innerLineCount = Max(minLineCount, Round(innerCircumference * precision));

            angleStep = arc / innerLineCount;
            previousPosition = new(center.X + Cos(fromAngle) * innerRadius, center.Y + Sin(fromAngle) * innerRadius,
                center.Z);
            for (var i = 1; i <= innerLineCount; ++i)
            {
                var angle = fromAngle + angleStep * i;
                Vector3 position = new(center.X + Cos(angle) * innerRadius, center.Y + Sin(angle) * innerRadius,
                    center.Z);
                line.Draw(previousPosition, position, color);
                previousPosition = position;
            }
        }
    }

    public static void DrawCone(this LineRenderer line, Vector2 center, float arc, float orientation, float innerRadius,
        float radius, Color color, float precision = 1)
        => line.DrawCone(new Vector3(center.X, center.Y, 0), arc, orientation, innerRadius, radius, color, precision);
}