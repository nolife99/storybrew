namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public static class CommandComparer
{
    public static int CompareCommands(ICommand x, ICommand y)
    {
        var result = float.Round(x.StartTime).CompareTo(float.Round(y.StartTime));
        if (result != 0) return result;
        return float.Round(x.EndTime).CompareTo(float.Round(y.EndTime));
    }
}