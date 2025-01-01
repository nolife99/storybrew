namespace StorybrewCommon.Curves;

using System.Collections.Generic;
using System.Linq;
using System.Numerics;

/// <summary>Represents a composite curve made up of multiple other curves.</summary>
public class CompositeCurve(IEnumerable<Curve> curves) : Curve
{
    readonly Curve[] curves = curves as Curve[] ?? curves.ToArray();

    /// <inheritdoc/>
    public Vector2 StartPosition => curves[0].StartPosition;

    /// <inheritdoc/>
    public Vector2 EndPosition => curves[^1].EndPosition;

    /// <inheritdoc/>
    public float Length => curves.Sum(c => c.Length);

    /// <inheritdoc/>
    public Vector2 PositionAtDistance(float distance)
    {
        foreach (var curve in curves)
        {
            if (distance < curve.Length) return curve.PositionAtDistance(distance);

            distance -= curve.Length;
        }

        return curves[^1].EndPosition;
    }

    /// <inheritdoc/>
    public Vector2 PositionAtDelta(float delta)
    {
        var length = Length;

        var d = delta;
        foreach (var curve in curves)
        {
            var curveDelta = curve.Length / length;
            if (d < curveDelta) return curve.PositionAtDelta(d / curveDelta);

            d -= curveDelta;
        }

        return curves[^1].EndPosition;
    }
}