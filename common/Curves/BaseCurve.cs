﻿using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Collections.Generic;

namespace StorybrewCommon.Curves;

///<summary> Represents a base curve. </summary>
[Serializable] public abstract class BaseCurve : Curve
{
    ///<inheritdoc/>
    public abstract CommandPosition EndPosition { get; }

    ///<inheritdoc/>
    public abstract CommandPosition StartPosition { get; }

    List<ValueTuple<float, CommandPosition>> distancePosition;

    double length;

    ///<inheritdoc/>
    public double Length
    {
        get
        {
            if (distancePosition == null) initialize();
            return length;
        }
    }

    void initialize()
    {
        distancePosition = [];
        Initialize(distancePosition, out length);
    }

    ///<summary/>
    protected abstract void Initialize(List<ValueTuple<float, CommandPosition>> distancePosition, out double length);

    ///<inheritdoc/>
    public CommandPosition PositionAtDistance(double distance)
    {
        if (distancePosition == null) initialize();

        var previousDistance = 0f;
        var previousPosition = StartPosition;

        var nextDistance = length;
        var nextPosition = EndPosition;

        var i = 0;
        while (i < distancePosition.Count)
        {
            var distancePositionTuple = distancePosition[i];
            if (distancePositionTuple.Item1 > distance) break;

            previousDistance = distancePositionTuple.Item1;
            previousPosition = distancePositionTuple.Item2;
            i++;
        }

        if (i < distancePosition.Count - 1)
        {
            var distancePositionTuple = distancePosition[i + 1];
            nextDistance = distancePositionTuple.Item1;
            nextPosition = distancePositionTuple.Item2;
        }

        var delta = (distance - previousDistance) / (nextDistance - previousDistance);
        var previousToNext = nextPosition - previousPosition;

        return previousPosition + previousToNext * (float)delta;
    }

    ///<inheritdoc/>
    public CommandPosition PositionAtDelta(double delta) => PositionAtDistance(delta * Length);
}