namespace StorybrewCommon.Curves;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

/// <summary>
///     Represents a bézier curve defined by a set of control points.
/// </summary>
public class BezierCurve(IEnumerable<Vector2> points, int precision) : BaseCurve
{
    [ThreadStatic] static Vector2[] intermediatePoints;
    readonly Vector2[] points = points as Vector2[] ?? points.ToArray();

    /// <inheritdoc/>
    public override Vector2 StartPosition => points[0];

    /// <inheritdoc/>
    public override Vector2 EndPosition => points[^1];

    /// <inheritdoc/>
    protected override void Initialize(List<(float, Vector2)> distancePosition, out float length)
    {
        var accuracy = points.Length > 2 ? precision : 0;

        var distance = 0f;
        var previousPosition = points[0];

        for (var i = 1f; i <= accuracy; ++i)
        {
            var delta = i / (accuracy + 1);
            ref var nextPosition = ref positionAtDelta(delta);

            distance += (nextPosition - previousPosition).Length();
            distancePosition.Add((distance, nextPosition));

            previousPosition = nextPosition;
        }

        distance += (points[^1] - previousPosition).Length();
        length = distance;
    }

    ref Vector2 positionAtDelta(float delta)
    {
        var count = points.Length;

        if (intermediatePoints is null || intermediatePoints.Length < count) intermediatePoints = new Vector2[count];

        for (var i = 0; i < count; ++i) intermediatePoints[i] = points[i];
        for (var i = 1; i < count; ++i)
        for (var j = 0; j < count - i; ++j)
            intermediatePoints[j] = intermediatePoints[j] * (1 - delta) + intermediatePoints[j + 1] * delta;

        return ref intermediatePoints[0];
    }
}