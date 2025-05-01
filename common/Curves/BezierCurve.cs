namespace StorybrewCommon.Curves;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Collections.Pooled;
using SixLabors.ImageSharp;

/// <summary>Represents a bézier curve defined by a set of control points.</summary>
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
        distancePosition.EnsureCapacity(distancePosition.Count + linearSegments.Length - 1);

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

        using var toFlatten = bSplineToBezierInternal(controlPoints, ref degree);

        Span<Vector2> subdivisionBuffer1 = stackalloc Vector2[degree + 1];
        Span<Vector2> subdivisionBuffer2 = stackalloc Vector2[degree * 2 + 1];

        while (toFlatten.Count > 0)
        {
            var parent = toFlatten.Pop();
            var parentSpan = parent.Memory.Span;

            if (bezierIsFlatEnough(parentSpan))
            {
                bezierApproximate(parentSpan, output, subdivisionBuffer1, subdivisionBuffer2, degree + 1);

                parent.Dispose();
                continue;
            }

            var rightChild = Configuration.Default.MemoryAllocator.Allocate<Vector2>(degree + 1);

            bezierSubdivide(parentSpan, subdivisionBuffer2, rightChild.Memory.Span, subdivisionBuffer1, degree + 1);

            subdivisionBuffer2[..(degree + 1)].CopyTo(parentSpan);

            toFlatten.Push(rightChild);
            toFlatten.Push(parent);
        }

        output.Add(controlPoints[pointCount]);
        return CollectionsMarshal.AsSpan(output);
    }

    static PooledStack<IMemoryOwner<Vector2>> bSplineToBezierInternal(ReadOnlySpan<Vector2> controlPoints, ref int degree)
    {
        PooledStack<IMemoryOwner<Vector2>> result = new();
        degree = Math.Min(degree, controlPoints.Length - 1);

        var pointCount = controlPoints.Length - 1;
        var points = Configuration.Default.MemoryAllocator.Allocate<Vector2>(controlPoints.Length);
        var pointsSpan = points.Memory.Span;
        controlPoints.CopyTo(pointsSpan);

        if (degree == pointCount) result.Push(points);
        else
        {
            for (var i = 0; i < pointCount - degree; i++)
            {
                var subBezier = Configuration.Default.MemoryAllocator.Allocate<Vector2>(degree + 1);
                var subBezierSpan = subBezier.Memory.Span;

                subBezierSpan[0] = pointsSpan[i];

                for (var j = 0; j < degree - 1; j++)
                {
                    subBezierSpan[j + 1] = pointsSpan[i + 1];

                    for (var k = 1; k < degree - j; k++)
                    {
                        var l = Math.Min(k, pointCount - degree - i);
                        pointsSpan[i + k] = (l * pointsSpan[i + k] + pointsSpan[i + k + 1]) / (l + 1);
                    }
                }

                subBezierSpan[degree] = pointsSpan[i + 1];
                result.Push(subBezier);
            }

            var pointSpan = pointsSpan[(pointCount - degree)..];
            var memoryOwner = Configuration.Default.MemoryAllocator.Allocate<Vector2>(pointSpan.Length);
            pointSpan.CopyTo(memoryOwner.Memory.Span);

            result.Push(memoryOwner);

            var old = result;
            result = new(old);
            old.Dispose();
        }

        return result;
    }

    static bool bezierIsFlatEnough(ReadOnlySpan<Vector2> controlPoints)
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

            for (var j = 0; j < count - i - 1; j++)
                subdivisionBuffer[j] = (subdivisionBuffer[j] + subdivisionBuffer[j + 1]) / 2;
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
            output.Add(
                .25f * (subdivisionBuffer2[index - 1] + 2 * subdivisionBuffer2[index] + subdivisionBuffer2[index + 1]));
        }
    }
}