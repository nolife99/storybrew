namespace StorybrewCommon.Storyboarding.Commands;

using System.Collections.Generic;

#pragma warning disable CS1591
public interface IFragmentableCommand : ICommand
{
    IFragmentableCommand GetFragment(float startTime, float endTime);
    IEnumerable<int> GetNonFragmentableTimes();
}