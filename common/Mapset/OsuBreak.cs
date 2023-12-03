﻿using System;
using System.Globalization;

namespace StorybrewCommon.Mapset;

#pragma warning disable CS1591
[Serializable] public class OsuBreak
{
    public double StartTime { get; internal set; }
    public double EndTime { get; internal set; }

    public override string ToString() => $"Break from {StartTime}ms to {EndTime}ms";
    public static OsuBreak Parse(string line)
    {
        var values = line.Split(',');
        return new()
        {
            StartTime = int.Parse(values[1], CultureInfo.InvariantCulture),
            EndTime = int.Parse(values[2], CultureInfo.InvariantCulture)
        };
    }
}