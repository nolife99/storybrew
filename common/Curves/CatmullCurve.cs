namespace StorybrewCommon.Curves;

using System.Collections.Generic;
using System.Numerics;

/// <summary> Represents a Catmull-Rom spline curve. </summary>
/// <remarks> Constructs a Catmull-Rom curve from given control points <paramref name="points" />. </remarks>
public class CatmullCurve(Vector2[] points, int precision) : BaseCurve
{
    /// <inheritdoc />
    public override Vector2 StartPosition => points[0];

    /// <inheritdoc />
    public override Vector2 EndPosition => points[^1];

    ///<summary> Whether the curve is straight (linear). </summary>
    public bool IsLinear => points.Length < 3;

    /// <summary />
    protected override void Initialize(List<(float, Vector2)> distancePosition, out float length)
    {
        var accuracy = points.Length > 2 ? precision : 0;

        var distance = 0f;
        var linePrecision = accuracy / points.Length;
        var previousPosition = StartPosition;

        for (var lineIndex = 0; lineIndex < points.Length - 1; ++lineIndex)
        for (var i = 1f; i <= linePrecision; ++i)
        {
            var nextPosition = positionAtDelta(
                lineIndex > 0 ? points[lineIndex - 1] : points[lineIndex], 
                points[lineIndex], points[lineIndex + 1], 
                lineIndex < points.Length - 2 ? points[lineIndex + 2] : points[lineIndex + 1], 
                i / (linePrecision + 1));

            distance += (nextPosition - previousPosition).Length();
            distancePosition.Add((distance, nextPosition));

            previousPosition = nextPosition;
        }

        distance += (EndPosition - previousPosition).Length();
        length = distance;
    }

    static Vector2 positionAtDelta(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float delta)
        => ((-p1 + 3 * p2 - 3 * p3 + p4) * delta * delta * delta + (2 * p1 - 5 * p2 + 4 * p3 - p4) * delta * delta +
            (-p1 + p3) * delta + 2 * p2) / 2;
}