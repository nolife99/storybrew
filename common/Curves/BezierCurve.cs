namespace StorybrewCommon.Curves;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>
///     Represents a bézier curve defined by a set of control points.
/// </summary>
public class BezierCurve(IEnumerable<Vector2> points) : BaseCurve
{
    const float BEZIER_TOLERANCE = .25f;

    readonly Vector2[] points = points as Vector2[] ?? points.ToArray();

    /// <inheritdoc/>
    public override Vector2 StartPosition => points[0];

    /// <inheritdoc/>
    public override Vector2 EndPosition => points[^1];

    /// <inheritdoc/>
    protected override void Initialize(List<(float, Vector2)> distancePosition, out float length)
    {
        var linearSegments = BSplineToPiecewiseLinear(points, points.Length - 1);

        length = 0;
        for (var i = 0; i < linearSegments.Length - 1; ++i)
        {
            var cur = linearSegments[i];

            distancePosition.Add((length, cur));
            length += Vector2.Distance(cur, linearSegments[i + 1]);
        }
    }

    // https://github.com/ppy/osu-framework/blob/master/osu.Framework/Utils/PathApproximator.cs
    static ReadOnlySpan<Vector2> BSplineToPiecewiseLinear(ReadOnlySpan<Vector2> controlPoints, int degree)
    {
        List<Vector2> output = [];
        var pointCount = controlPoints.Length - 1;

        var toFlatten = bSplineToBezierInternal(controlPoints, ref degree);
        Stack<Vector2[]> freeBuffers = new();

        var subdivisionBuffer1 = new Vector2[degree + 1];
        var subdivisionBuffer2 = new Vector2[degree * 2 + 1];

        while (toFlatten.Count > 0)
        {
            var parent = toFlatten.Pop();

            if (bezierIsFlatEnough(parent))
            {
                bezierApproximate(parent, output, subdivisionBuffer1, subdivisionBuffer2, degree + 1);

                freeBuffers.Push(parent);
                continue;
            }

            var rightChild = freeBuffers.Count > 0 ? freeBuffers.Pop() : new Vector2[degree + 1];
            bezierSubdivide(parent, subdivisionBuffer2, rightChild, subdivisionBuffer1, degree + 1);

            subdivisionBuffer2.AsSpan(0, degree + 1).CopyTo(parent);

            toFlatten.Push(rightChild);
            toFlatten.Push(parent);
        }

        output.Add(controlPoints[pointCount]);
        return CollectionsMarshal.AsSpan(output);
    }
    static Stack<Vector2[]> bSplineToBezierInternal(ReadOnlySpan<Vector2> controlPoints, ref int degree)
    {
        Stack<Vector2[]> result = new();
        degree = Math.Min(degree, controlPoints.Length - 1);

        var pointCount = controlPoints.Length - 1;
        var points = controlPoints.ToArray();

        if (degree == pointCount) result.Push(points);
        else
        {
            for (var i = 0; i < pointCount - degree; i++)
            {
                var subBezier = new Vector2[degree + 1];
                subBezier[0] = points[i];

                for (var j = 0; j < degree - 1; j++)
                {
                    subBezier[j + 1] = points[i + 1];

                    for (var k = 1; k < degree - j; k++)
                    {
                        var l = Math.Min(k, pointCount - degree - i);
                        points[i + k] = (l * points[i + k] + points[i + k + 1]) / (l + 1);
                    }
                }

                subBezier[degree] = points[i + 1];
                result.Push(subBezier);
            }

            result.Push(points[(pointCount - degree)..]);

            result = new(result);
        }

        return result;
    }

    static bool bezierIsFlatEnough(Vector2[] controlPoints)
    {
        for (var i = 1; i < controlPoints.Length - 1; i++)
            if ((controlPoints[i - 1] - 2 * controlPoints[i] + controlPoints[i + 1]).LengthSquared() >
                BEZIER_TOLERANCE * BEZIER_TOLERANCE * 4)
                return false;

        return true;
    }

    static void bezierSubdivide(ReadOnlySpan<Vector2> controlPoints,
        Span<Vector2> l,
        Span<Vector2> r,
        Span<Vector2> subdivisionBuffer,
        int count)
    {
        controlPoints[..count].CopyTo(subdivisionBuffer);
        for (var i = 0; i < count; ++i)
        {
            l[i] = subdivisionBuffer[0];
            r[count - i - 1] = subdivisionBuffer[count - i - 1];

            for (var j = 0; j < count - i - 1; j++) subdivisionBuffer[j] = (subdivisionBuffer[j] + subdivisionBuffer[j + 1]) / 2;
        }
    }

    static void bezierApproximate(ReadOnlySpan<Vector2> controlPoints,
        List<Vector2> output,
        Span<Vector2> subdivisionBuffer1,
        Span<Vector2> subdivisionBuffer2,
        int count)
    {
        bezierSubdivide(controlPoints, subdivisionBuffer2, subdivisionBuffer1, subdivisionBuffer1, count);

        for (var i = 0; i < count - 1; ++i) subdivisionBuffer2[count + i] = subdivisionBuffer1[i + 1];

        output.Add(controlPoints[0]);

        for (var i = 1; i < count - 1; ++i)
        {
            var index = 2 * i;
            output.Add(.25f * (subdivisionBuffer2[index - 1] + 2 * subdivisionBuffer2[index] + subdivisionBuffer2[index + 1]));
        }
    }
}