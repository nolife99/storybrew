namespace StorybrewCommon.Curves;

using System;
using System.Collections.Generic;
using System.Numerics;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

/// <summary>Represents a bézier curve defined by a set of control points.</summary>
public class BezierCurve(scoped ReadOnlySpan<Vector2> points) : BaseCurve
{
    const float BEZIER_TOLERANCE = .25f;

    readonly Vector2[] points = points.ToArray();

    /// <inheritdoc/>
    public override Vector2 StartPosition => points[0];

    /// <inheritdoc/>
    public override Vector2 EndPosition => points[^1];

    /// <inheritdoc/>
    protected override void Initialize(List<(float, Vector2)> distancePosition, out float length)
    {
        using var linearSegments = BSplineToPiecewiseLinear(points, points.Length - 1);
        distancePosition.EnsureCapacity(distancePosition.Count + linearSegments.Count - 1);

        length = 0;
        for (var i = 0; i < linearSegments.Count - 1; ++i)
        {
            var cur = linearSegments[i];

            distancePosition.Add((length, cur));
            length += Vector2.Distance(cur, linearSegments[i + 1]);
        }
    }

    // https://github.com/ppy/osu-framework/blob/master/osu.Framework/Utils/PathApproximator.cs
    static TempList<Vector2> BSplineToPiecewiseLinear(scoped ReadOnlySpan<Vector2> controlPoints, int degree)
    {
        var output = TempList.Create<Vector2>();
        var pointCount = controlPoints.Length - 1;

        using var toFlatten = bSplineToBezierInternal(controlPoints, ref degree);

        using var subdivisionBuffer1 = ValueArray.Create<Vector2>(degree + 1);
        using var subdivisionBuffer2 = ValueArray.Create<Vector2>(degree * 2 + 1);

        while (toFlatten.Count > 0)
        {
            var parent = toFlatten.Pop();
            if (bezierIsFlatEnough(parent))
            {
                bezierApproximate(parent, ref output, subdivisionBuffer1, subdivisionBuffer2, degree + 1);

                parent.Dispose();
                continue;
            }

            var rightChild = ValueArray.Create<Vector2>(degree + 1);

            bezierSubdivide(parent, subdivisionBuffer2, rightChild, subdivisionBuffer1, degree + 1);

            subdivisionBuffer2.AsReadOnlySpan(..(degree + 1)).CopyTo(parent.AsSpan());

            toFlatten.Push(rightChild);
            toFlatten.Push(parent);
        }

        output.Add(controlPoints[pointCount]);
        return output;
    }

    static TempStack<ValueArray<Vector2>> bSplineToBezierInternal(scoped ReadOnlySpan<Vector2> controlPoints, ref int degree)
    {
        var result = TempStack.Create<ValueArray<Vector2>>();
        degree = int.Min(degree, controlPoints.Length - 1);

        var pointCount = controlPoints.Length - 1;
        var points = ValueArray.Create(controlPoints);

        if (degree == pointCount) result.Push(points);
        else
        {
            for (var i = 0; i < pointCount - degree; ++i)
            {
                var subBezier = ValueArray.Create<Vector2>(degree + 1);
                subBezier[0] = points[i];

                for (var j = 0; j < degree - 1; j++)
                {
                    subBezier[j + 1] = points[i + 1];

                    for (var k = 1; k < degree - j; k++)
                    {
                        var l = int.Min(k, pointCount - degree - i);
                        points[i + k] = (l * points[i + k] + points[i + k + 1]) / (l + 1);
                    }
                }

                subBezier[degree] = points[i + 1];
                result.Push(subBezier);
            }

            var memoryOwner = ValueArray.Create(points.AsReadOnlySpan((pointCount - degree)..));
            points.Dispose();
            result.Push(memoryOwner);

            using var old = result;
            result = TempStack.Create(old.AsReadOnlySpan());
        }

        return result;
    }

    static bool bezierIsFlatEnough(ValueArray<Vector2> controlPoints)
    {
        for (var i = 1; i < controlPoints.Length - 1; i++)
            if ((controlPoints[i - 1] - 2 * controlPoints[i] + controlPoints[i + 1]).LengthSquared() >
                BEZIER_TOLERANCE * BEZIER_TOLERANCE * 4)
                return false;

        return true;
    }

    static void bezierSubdivide(ValueArray<Vector2> controlPoints,
        ValueArray<Vector2> l,
        ValueArray<Vector2> r,
        ValueArray<Vector2> subdivisionBuffer,
        int count)
    {
        controlPoints.AsReadOnlySpan(..count).CopyTo(subdivisionBuffer.AsSpan());
        for (var i = 0; i < count; ++i)
        {
            l[i] = subdivisionBuffer[0];
            r[count - i - 1] = subdivisionBuffer[count - i - 1];

            for (var j = 0; j < count - i - 1; j++)
                subdivisionBuffer[j] = (subdivisionBuffer[j] + subdivisionBuffer[j + 1]) / 2;
        }
    }

    static void bezierApproximate(ValueArray<Vector2> controlPoints,
        scoped ref TempList<Vector2> output,
        ValueArray<Vector2> subdivisionBuffer1,
        ValueArray<Vector2> subdivisionBuffer2,
        int count)
    {
        bezierSubdivide(controlPoints, subdivisionBuffer2, subdivisionBuffer1, subdivisionBuffer1, count);

        for (var i = 0; i < count - 1; ++i) subdivisionBuffer2[count + i] = subdivisionBuffer1[i + 1];

        output.Add(controlPoints[0]);

        for (var i = 1; i < count - 1; ++i)
        {
            var index = 2 * i;
            output.Add(
                .25f * (subdivisionBuffer2[index - 1] + 2 * subdivisionBuffer2[index] + subdivisionBuffer2[index + 1]));
        }
    }
}