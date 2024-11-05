namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public interface ITypedCommand<TValue> : ICommand
{
    OsbEasing Easing { get; }
    TValue StartValue { get; }
    TValue EndValue { get; }
    float Duration { get; }

    TValue ValueAtTime(float time);
}