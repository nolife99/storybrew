using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Collections.Generic;

namespace StorybrewCommon.Curves;

///<summary> Represents a Catmull-Rom spline curve. </summary>
///<remarks> Constructs a Catmull-Rom curve from given control points <paramref name="points"/>. </remarks>
[Serializable] public class CatmullCurve(CommandPosition[] points, int precision) : BaseCurve
{
    ///<inheritdoc/>
    public override CommandPosition StartPosition => points[0];

    ///<inheritdoc/>
    public override CommandPosition EndPosition => points[^1];

    ///<summary> Whether the curve is straight (linear). </summary>
    public bool IsLinear => points.Length < 3;

    ///<summary/>
    protected override void Initialize(List<(float, CommandPosition)> distancePosition, out double length)
    {
        var accuracy = points.Length > 2 ? precision : 0;

        var distance = 0f;
        var linePrecision = accuracy / points.Length;
        var previousPosition = StartPosition;

        for (var lineIndex = 0; lineIndex < points.Length - 1; ++lineIndex) for (var i = 1; i <= linePrecision; ++i)
        {
            var delta = (float)i / (linePrecision + 1);

            var p1 = lineIndex > 0 ? points[lineIndex - 1] : points[lineIndex];
            var p2 = points[lineIndex];
            var p3 = points[lineIndex + 1];
            var p4 = lineIndex < points.Length - 2 ? points[lineIndex + 2] : points[lineIndex + 1];

            var nextPosition = positionAtDelta(p1, p2, p3, p4, delta);

            distance += (nextPosition - previousPosition).Length;
            distancePosition.Add((distance, nextPosition));

            previousPosition = nextPosition;
        }
        distance += (EndPosition - previousPosition).Length;
        length = distance;
    }

    static CommandPosition positionAtDelta(CommandPosition p1, CommandPosition p2, CommandPosition p3, CommandPosition p4, float delta)
        => ((-p1 + 3 * p2 - 3 * p3 + p4) * delta * delta * delta + (2 * p1 - 5 * p2 + 4 * p3 - p4) * delta * delta + (-p1 + p3) * delta + 2 * p2) / 2;
}