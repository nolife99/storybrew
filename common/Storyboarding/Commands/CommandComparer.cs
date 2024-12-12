namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public static class CommandComparer
{
    public static int CompareCommands(ICommand x, ICommand y)
    {
        var result = float.Round(x.StartTime).CompareTo(float.Round(y.StartTime));
        return result != 0 ? result : float.Round(x.EndTime).CompareTo(float.Round(y.EndTime));
    }
}