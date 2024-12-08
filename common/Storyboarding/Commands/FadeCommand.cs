namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public record FadeCommand(OsbEasing easing, float startTime, float endTime, CommandDecimal startValue, CommandDecimal endValue)
    : Command<CommandDecimal>("F", easing, startTime, endTime, startValue, endValue)
{
    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandDecimal Midpoint(Command<CommandDecimal> endCommand, float progress)
        => StartValue + (endCommand.EndValue - StartValue) * progress;
    public override IFragmentableCommand GetFragment(float startTime, float endTime) => IsFragmentable ?
        new FadeCommand(Easing, startTime, endTime, ValueAtTime(startTime), ValueAtTime(endTime)) :
        this;
}