using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace StorybrewCommon.Curves;

#pragma warning disable CS1591
///<summary> Constructs a bézier curve from a list of points <paramref name="points"/>. </summary>
public class BezierCurve(IEnumerable<Vector2> points, int precision) : BaseCurve
{
    readonly Vector2[] points = points as Vector2[] ?? points.ToArray();

    ///<summary> The start position (the head) of the bézier curve. </summary>
    public override Vector2 StartPosition => points[0];

    ///<summary> The end position (the tail) of the bézier curve. </summary>
    public override Vector2 EndPosition => points[^1];

    ///<summary> Whether the bézier curve is straight (linear). </summary>
    public bool IsLinear => points.Length < 3;

    protected override void Initialize(List<(float, Vector2)> distancePosition, out float length)
    {
        var accuracy = points.Length > 2 ? precision : 0;

        var distance = 0f;
        var previousPosition = StartPosition;

        for (var i = 1f; i <= accuracy; ++i)
        {
            var delta = i / (accuracy + 1);
            var nextPosition = positionAtDelta(delta);

            distance += (nextPosition - previousPosition).Length();
            distancePosition.Add((distance, nextPosition));

            previousPosition = nextPosition;
        }
        distance += (EndPosition - previousPosition).Length();
        length = distance;
    }

    [ThreadStatic] static Vector2[] intermediatePoints;
    Vector2 positionAtDelta(float delta)
    {
        var count = points.Length;

        if (intermediatePoints is null || intermediatePoints.Length < count) intermediatePoints = new Vector2[count];

        for (var i = 0; i < count; ++i) intermediatePoints[i] = points[i];
        for (var i = 1; i < count; ++i) for (var j = 0; j < count - i; ++j) intermediatePoints[j] = intermediatePoints[j] * (1 - delta) + intermediatePoints[j + 1] * delta;

        return intermediatePoints[0];
    }
}