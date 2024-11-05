﻿using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace StorybrewCommon.Curves;

///<summary> Represents a composite curve that is constructed from multiple curves. </summary>
///<remarks> Constructs a composite curve from a list of curves <paramref name="curves"/>. </remarks>
public class CompositeCurve(IEnumerable<Curve> curves) : Curve
{
    readonly Curve[] curves = (curves as Curve[]) ?? curves.ToArray();

    ///<summary> Returns a readonly list of curves that makes up the composite curve. </summary>
    public IReadOnlyList<Curve> Curves => curves;

    ///<inheritdoc/>
    public Vector2 StartPosition => curves[0].StartPosition;

    ///<inheritdoc/>
    public Vector2 EndPosition => curves[^1].EndPosition;

    ///<inheritdoc/>
    public float Length
    {
        get
        {
            var length = curves[0].Length;
            for (var i = 1; i < curves.Length; ++i) length += curves[i].Length;
            return length;
        }
    }

    ///<inheritdoc/>
    public Vector2 PositionAtDistance(float distance)
    {
        for (var i = 0; i < curves.Length; ++i)
        {
            var curve = curves[i];
            if (distance < curve.Length) return curve.PositionAtDistance(distance);
            distance -= curve.Length;
        }
        return curves[^1].EndPosition;
    }

    ///<inheritdoc/>
    public Vector2 PositionAtDelta(float delta)
    {
        var length = Length;

        var d = delta;
        for (var i = 0; i < curves.Length; ++i)
        {
            var curve = curves[i];
            var curveDelta = curve.Length / length;

            if (d < curveDelta) return curve.PositionAtDelta(d / curveDelta);
            d -= curveDelta;
        }
        return EndPosition;
    }
}