namespace StorybrewCommon.Mapset;

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