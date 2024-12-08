namespace StorybrewCommon.Curves;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>
///     Represents a Catmull-Rom curve defined by a set of control points.
/// </summary>
public class CatmullCurve(Vector2[] points) : BaseCurve
{
    const int catmull_detail = 50;

    /// <inheritdoc/>
    public override Vector2 StartPosition => points[0];

    /// <inheritdoc/>
    public override Vector2 EndPosition => points[^1];

    /// <summary/>
    protected override void Initialize(List<(float, Vector2)> distancePosition, out float length)
    {
        var linearSegments = CatmullToPiecewiseLinear(points);

        length = 0;
        for (var i = 0; i < linearSegments.Length - 1; ++i)
        {
            var cur = linearSegments[i];
            var next = linearSegments[i + 1];
            var dist = Vector2.Distance(cur, next);

            distancePosition.Add((length, cur));
            length += dist;
        }
    }

    // https://github.com/ppy/osu-framework/blob/master/osu.Framework/Utils/PathApproximator.cs
    static ReadOnlySpan<Vector2> CatmullToPiecewiseLinear(ReadOnlySpan<Vector2> controlPoints)
    {
        List<Vector2> result = new((controlPoints.Length - 1) * catmull_detail * 2);

        for (var i = 0; i < controlPoints.Length - 1; i++)
        {
            var v1 = i > 0 ? controlPoints[i - 1] : controlPoints[i];
            var v2 = controlPoints[i];
            var v3 = i < controlPoints.Length - 1 ? controlPoints[i + 1] : v2 + v2 - v1;
            var v4 = i < controlPoints.Length - 2 ? controlPoints[i + 2] : v3 + v3 - v2;

            for (var c = 0; c < catmull_detail; c++)
            {
                result.Add(catmullFindPoint(ref v1, ref v2, ref v3, ref v4, (float)c / catmull_detail));
                result.Add(catmullFindPoint(ref v1, ref v2, ref v3, ref v4, (float)(c + 1) / catmull_detail));
            }
        }

        return CollectionsMarshal.AsSpan(result);
    }

    static Vector2 catmullFindPoint(ref Vector2 vec1, ref Vector2 vec2, ref Vector2 vec3, ref Vector2 vec4, float t)
    {
        var t2 = t * t;
        var t3 = t * t2;

        Vector2 result;
        result.X = .5f * (2 * vec2.X + (-vec1.X + vec3.X) * t + (2 * vec1.X - 5 * vec2.X + 4 * vec3.X - vec4.X) * t2 + (-vec1.X + 3f * vec2.X - 3f * vec3.X + vec4.X) * t3);
        result.Y = .5f * (2 * vec2.Y + (-vec1.Y + vec3.Y) * t + (2 * vec1.Y - 5 * vec2.Y + 4 * vec3.Y - vec4.Y) * t2 + (-vec1.Y + 3f * vec2.Y - 3f * vec3.Y + vec4.Y) * t3);

        return result;
    }
}