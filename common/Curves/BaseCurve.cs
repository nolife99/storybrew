using System;
using System.Collections.Generic;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Curves;

///<summary> Represents a base curve. </summary>
[Serializable] public abstract class BaseCurve : Curve
{
    ///<inheritdoc/>
    public abstract CommandPosition EndPosition { get; }

    ///<inheritdoc/>
    public abstract CommandPosition StartPosition { get; }

    List<(float Distance, CommandPosition Position)> distancePosition;

    double length;

    ///<inheritdoc/>
    public double Length
    {
        get
        {
            if (distancePosition is null) initialize();
            return length;
        }
    }

    void initialize()
    {
        distancePosition = [];
        Initialize(distancePosition, out length);
    }

    ///<summary/>
    protected abstract void Initialize(List<(float, CommandPosition)> distancePosition, out double length);

    ///<inheritdoc/>
    public CommandPosition PositionAtDistance(double distance)
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

        return previousPosition + previousToNext * (float)delta;
    }

    ///<inheritdoc/>
    public CommandPosition PositionAtDelta(double delta) => PositionAtDistance(delta * Length);
}