namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public interface ITypedCommand<TValue> : ICommand
{
    TValue StartValue { get; }
    TValue EndValue { get; }
    TValue ValueAtTime(float time);
}