using System;

namespace StorybrewCommon.Storyboarding.Commands
{
#pragma warning disable CS1591
    public static class CommandComparer
    {
        public static int CompareCommands(ICommand x, ICommand y)
        {
            var result = ((int)Math.Round(x.StartTime)).CompareTo((int)Math.Round(y.StartTime));
            if (result != 0) return result;
            return ((int)Math.Round(x.EndTime)).CompareTo((int)Math.Round(y.EndTime));
        }
    }
}