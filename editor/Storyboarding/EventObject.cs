namespace StorybrewEditor.Storyboarding;

public interface EventObject
{
    float EventTime { get; }
    void TriggerEvent(Project project, float currentTime);
}