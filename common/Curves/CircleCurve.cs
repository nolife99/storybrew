namespace StorybrewCommon.Curves;

using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
///     Represents a circular arc curve defined by three control points: a start point, a midpoint, and an end point.
/// </summary>
public class CircleCurve(Vector2 startPoint, Vector2 midPoint, Vector2 endPoint) : BaseCurve
{
    /// <inheritdoc/>
    public override Vector2 StartPosition => startPoint;

    /// <inheritdoc/>
    public override Vector2 EndPosition => endPoint;

    /// <summary/>
    protected override void Initialize(List<(float, Vector2)> distancePosition, out float length)
    {
        var d = 2 * (startPoint.X * (midPoint.Y - endPoint.Y) + midPoint.X * (endPoint.Y - startPoint.Y) +
            endPoint.X * (startPoint.Y - midPoint.Y));

        if (d == 0) throw new ArgumentException("Invalid circle curve");

        var startPointLS = startPoint.LengthSquared();
        var midPointLS = midPoint.LengthSquared();
        var endPointLS = endPoint.LengthSquared();

        Vector2 centre =
            new(
                (startPointLS * (midPoint.Y - endPoint.Y) + midPointLS * (endPoint.Y - startPoint.Y) +
                    endPointLS * (startPoint.Y - midPoint.Y)) / d,
                (startPointLS * (endPoint.X - midPoint.X) + midPointLS * (startPoint.X - endPoint.X) +
                    endPointLS * (midPoint.X - startPoint.X)) / d);

        var radius = (startPoint - centre).Length();

        var startAngle = float.Atan2(startPoint.Y - centre.Y, startPoint.X - centre.X);
        var midAngle = float.Atan2(midPoint.Y - centre.Y, midPoint.X - centre.X);
        var endAngle = float.Atan2(endPoint.Y - centre.Y, endPoint.X - centre.X);

        while (midAngle < startAngle) midAngle += float.Tau;
        while (endAngle < startAngle) endAngle += float.Tau;
        if (midAngle > endAngle) endAngle -= float.Tau;

        length = Math.Abs((endAngle - startAngle) * radius);
        var precision = (int)(length / 4);

        for (var i = 1f; i < length; ++i)
        {
            var progress = i / precision;
            var (sin, cos) = float.SinCos(endAngle * progress + startAngle * (1 - progress));
            distancePosition.Add((progress * length, new Vector2(cos * radius, sin * radius) + centre));
        }

        distancePosition.Add((length, endPoint));
    }

    ///<summary> Returns whether or not the curve is a valid circle curve based on given control points. </summary>
    public static bool IsValid(Vector2 startPoint, Vector2 midPoint, Vector2 endPoint) => startPoint != midPoint &&
        midPoint != endPoint && 2 * (startPoint.X * (midPoint.Y - endPoint.Y) + midPoint.X * (endPoint.Y - startPoint.Y) +
            endPoint.X * (startPoint.Y - midPoint.Y)) != 0;
}