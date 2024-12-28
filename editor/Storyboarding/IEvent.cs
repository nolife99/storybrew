namespace StorybrewEditor.Storyboarding;

public interface IEvent
{
    float EventTime { get; }
    void TriggerEvent(Project project, float currentTime);
}