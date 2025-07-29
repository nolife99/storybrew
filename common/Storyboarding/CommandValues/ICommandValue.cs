namespace StorybrewCommon.Storyboarding.CommandValues;

using Tiny.PooledCollections.Generic.Temporary;

/// <summary> Represents a common interface for different types of command values used in storyboarding. </summary>
public interface ICommandValue
{
    internal TempList<char> ToOsbString(ExportSettings exportSettings);
}