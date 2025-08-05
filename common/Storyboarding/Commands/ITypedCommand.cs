namespace StorybrewCommon.Storyboarding.Commands;

using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding.Display;

/// <summary> Represents a command that operates on a specific type of value. </summary>
/// <typeparam name="TValue"> The type of value that this command changes over time. </typeparam>
public interface ITypedCommand<TValue> : ICommand where TValue : struct, ICommandValue
{
    /// <summary> The start value of the command. </summary>
    TValue StartValue { get; }

    /// <summary> The end value of the command. </summary>
    TValue EndValue { get; }

    /// <summary> Gets the value of the command at the given time. </summary>
    TValue ValueAtTime(float time);

    /// <summary> Converts the command to a <see cref="CommandResult{TValue}"/> with the given time offset. </summary>
    CommandResult<TValue> AsResult(float timeOffset = 0);
}