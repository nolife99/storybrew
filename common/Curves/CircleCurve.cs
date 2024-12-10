namespace StorybrewCommon.Curves;

using System;
using System.Collections.Generic;
using System.Numerics;
using BrewLib.Util;

/// <summary>
///     Represents a circular arc curve defined by three control points: a start point, a midpoint, and an end point.
/// </summary>
public class CircleCurve(Vector2 startPoint, Vector2 midPoint, Vector2 endPoint) : BaseCurve
{
    const float circular_arc_tolerance = .1f;

    /// <inheritdoc/>
    public override Vector2 StartPosition => startPoint;

    /// <inheritdoc/>
    public override Vector2 EndPosition => endPoint;

    /// <summary/>
    protected override void Initialize(List<(float, Vector2)> distancePosition, out float length)
    {
        using var linearSegmentsBuf = CircularArcToPiecewiseLinear([startPoint, midPoint, endPoint]);
        var linearSegments = linearSegmentsBuf.GetSpan();

        length = 0;
        for (var i = 0; i < linearSegments.Length - 1; ++i)
        {
            var cur = linearSegments[i];

            distancePosition.Add((length, cur));
            length += Vector2.Distance(cur, linearSegments[i + 1]);
        }
    }

    ///<summary> Returns whether or not the curve is a valid circle curve based on given control points. </summary>
    public static bool IsValid(Vector2 startPoint, Vector2 midPoint, Vector2 endPoint) => startPoint != midPoint &&
        midPoint != endPoint && 2 * (startPoint.X * (midPoint.Y - endPoint.Y) + midPoint.X * (endPoint.Y - startPoint.Y) +
            endPoint.X * (startPoint.Y - midPoint.Y)) != 0;

    // https://github.com/ppy/osu-framework/blob/master/osu.Framework/Utils/PathApproximator.cs
    static UnmanagedBuffer<Vector2> CircularArcToPiecewiseLinear(ReadOnlySpan<Vector2> controlPoints)
    {
        CircularArcProperties pr = new(controlPoints);
        var amountPoints = 2 * pr.Radius <= circular_arc_tolerance ?
            2 :
            Math.Max(2, (int)MathF.Ceiling(pr.ThetaRange / (2 * MathF.Acos(1 - circular_arc_tolerance / pr.Radius))));

        UnmanagedBuffer<Vector2> output = new(amountPoints);
        var outputSpan = output.GetSpan();

        for (var i = 0; i < amountPoints; ++i)
        {
            var fract = i / (amountPoints - 1f);
            var (sin, cos) = MathF.SinCos(pr.ThetaStart + pr.Direction * fract * pr.ThetaRange);
            outputSpan[i] = pr.Centre + new Vector2(cos, sin) * pr.Radius;
        }

        return output;
    }

    readonly struct CircularArcProperties
    {
        public readonly float ThetaStart;
        public readonly float ThetaRange;
        public readonly float Direction;
        public readonly float Radius;
        public readonly Vector2 Centre;

        public CircularArcProperties(ReadOnlySpan<Vector2> controlPoints)
        {
            var a = controlPoints[0];
            var b = controlPoints[1];
            var c = controlPoints[2];
            var d = 2 * (a.X * (b - c).Y + b.X * (c - a).Y + c.X * (a - b).Y);

            var aSq = a.LengthSquared();
            var bSq = b.LengthSquared();
            var cSq = c.LengthSquared();

            Centre = new Vector2(aSq * (b - c).Y + bSq * (c - a).Y + cSq * (a - b).Y,
                aSq * (c - b).X + bSq * (a - c).X + cSq * (b - a).X) / d;

            var dA = a - Centre;
            var dC = c - Centre;

            Radius = dA.Length();

            ThetaStart = MathF.Atan2(dA.Y, dA.X);
            var thetaEnd = MathF.Atan2(dC.Y, dC.X);

            while (thetaEnd < ThetaStart) thetaEnd += MathF.Tau;

            Direction = 1;
            ThetaRange = thetaEnd - ThetaStart;

            var orthoAtoC = c - a;
            orthoAtoC = new Vector2(orthoAtoC.Y, -orthoAtoC.X);

            if (Vector2.Dot(orthoAtoC, b - a) < 0)
            {
                Direction = -Direction;
                ThetaRange = MathF.Tau - ThetaRange;
            }
        }
    }
}