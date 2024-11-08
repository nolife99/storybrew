namespace StorybrewCommon.Curves;

using System.Collections.Generic;
using System.Numerics;

///<summary> Represents a base curve. </summary>
public abstract class BaseCurve : Curve
{
    List<(float Distance, Vector2 Position)> distancePosition;

    float length;

    /// <inheritdoc />
    public abstract Vector2 EndPosition { get; }

    /// <inheritdoc />
    public abstract Vector2 StartPosition { get; }

    /// <inheritdoc />
    public float Length
    {
        get
        {
            if (distancePosition is null) initialize();
            return length;
        }
    }

    /// <inheritdoc />
    public Vector2 PositionAtDistance(float distance)
    {
        if (distancePosition is null) initialize();

        var previousDistance = 0f;
        var previousPosition = StartPosition;

        var nextDistance = length;
        var nextPosition = EndPosition;

        var i = 0;
        while (i < distancePosition.Count)
        {
            var (Distance, Position) = distancePosition[i];
            if (Distance > distance) break;

            previousDistance = Distance;
            previousPosition = Position;
            ++i;
        }

        if (i < distancePosition.Count - 1)
        {
            var (Distance, Position) = distancePosition[i + 1];
            nextDistance = Distance;
            nextPosition = Position;
        }

        var delta = (distance - previousDistance) / (nextDistance - previousDistance);
        var previousToNext = nextPosition - previousPosition;

        return previousPosition + previousToNext * delta;
    }

    /// <inheritdoc />
    public Vector2 PositionAtDelta(float delta) => PositionAtDistance(delta * Length);

    void initialize()
    {
        distancePosition = [];
        Initialize(distancePosition, out length);
    }

    /// <summary />
    protected abstract void Initialize(List<(float, Vector2)> distancePosition, out float length);
}