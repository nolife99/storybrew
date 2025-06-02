namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;
using Display;

#pragma warning disable CS1591
public interface ITypedCommand<TValue> : ICommand where TValue : struct, ICommandValue
{
    TValue StartValue { get; }
    TValue EndValue { get; }
    TValue ValueAtTime(float time);

    CommandResult<TValue> AsResult(float timeOffset = 0);
}