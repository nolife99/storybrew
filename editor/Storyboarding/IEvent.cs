namespace StorybrewEditor.Storyboarding;

using System;

public interface IEvent
{
    TimeSpan EventTime { get; }
    void TriggerEvent(Project project, TimeSpan currentTime);
}