namespace StorybrewCommon.Storyboarding.Commands;

using System;

#pragma warning disable CS1591
public static class CommandComparer
{
    public static int CompareCommands(ICommand x, ICommand y)
    {
        var result = MathF.Round(x.StartTime).CompareTo(MathF.Round(y.StartTime));
        if (result != 0) return result;
        return MathF.Round(x.EndTime).CompareTo(MathF.Round(y.EndTime));
    }
}