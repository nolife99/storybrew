namespace StorybrewCommon.Storyboarding.CommandValues;

#pragma warning disable CS1591
public interface CommandValue
{
    string ToOsbString(ExportSettings exportSettings);
    int GetHashCode();
}