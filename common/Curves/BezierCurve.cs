using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StorybrewCommon.Curves;

#pragma warning disable CS1591
///<summary> Constructs a bézier curve from a list of points <paramref name="points"/>. </summary>
[Serializable] public class BezierCurve(IEnumerable<CommandPosition> points, int precision) : BaseCurve
{
    readonly CommandPosition[] points = points as CommandPosition[] ?? points.ToArray();
    readonly int precision = precision;

    ///<summary> The start position (the head) of the bézier curve. </summary>
    public override CommandPosition StartPosition => points[0];

    ///<summary> The end position (the tail) of the bézier curve. </summary>
    public override CommandPosition EndPosition => points[^1];

    ///<summary> Whether the bézier curve is straight (linear). </summary>
    public bool IsLinear => points.Length < 3;

    protected override void Initialize(List<ValueTuple<float, CommandPosition>> distancePosition, out double length)
    {
        var precision = points.Length > 2 ? this.precision : 0;

        var distance = 0f;
        var previousPosition = StartPosition;

        for (var i = 1f; i <= precision; ++i)
        {
            var delta = i / (precision + 1);
            var nextPosition = positionAtDelta(delta);

            distance += (nextPosition - previousPosition).Length;
            distancePosition.Add(new ValueTuple<float, CommandPosition>(distance, nextPosition));

            previousPosition = nextPosition;
        }
        distance += (EndPosition - previousPosition).Length;
        length = distance;
    }

    [ThreadStatic] static CommandPosition[] intermediatePoints;
    CommandPosition positionAtDelta(float delta)
    {
        var pointsCount = points.Length;

        if (intermediatePoints == null || intermediatePoints.Length < pointsCount) intermediatePoints = new CommandPosition[pointsCount];

        for (var i = 0; i < pointsCount; ++i) intermediatePoints[i] = points[i];
        for (var i = 1; i < pointsCount; ++i) for (var j = 0; j < pointsCount - i; ++j) intermediatePoints[j] =
            intermediatePoints[j] * (1 - delta) + intermediatePoints[j + 1] * delta;

        return intermediatePoints[0];
    }
}