namespace StorybrewCommon.Mapset;

using System;
using System.Globalization;

/// <summary>Represents an osu! break, a time period without hit objects.</summary>
public class OsuBreak
{
    /// <summary>The time that the break starts.</summary>
    public int StartTime { get; internal set; }

    /// <summary>The time that the break end.</summary>
    public int EndTime { get; internal set; }

    /// <inheritdoc/>
    public override string ToString() => $"Break from {StartTime}ms to {EndTime}ms";

    ///<summary> Parses an osu! break from a given line. </summary>
    public static OsuBreak Parse(ReadOnlySpan<char> line)
    {
        var values = line.Split(',');

        values.MoveNext();
        values.MoveNext();

        var startTime = values.Current;

        values.MoveNext();
        var endTime = values.Current;

        return new()
        {
            StartTime = int.Parse(line[startTime], CultureInfo.InvariantCulture),
            EndTime = int.Parse(line[endTime], CultureInfo.InvariantCulture)
        };
    }
}