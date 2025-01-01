namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public record ColorCommand(OsbEasing easing, float startTime, float endTime, CommandColor startValue, CommandColor endValue)
    : Command<CommandColor>("C", easing, startTime, endTime, startValue, endValue)
{
    public override CommandColor ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;

    public override CommandColor Midpoint(Command<CommandColor> endCommand, float progress)
        => StartValue + (endCommand.EndValue - StartValue) * progress;

    public override IFragmentableCommand GetFragment(float startTime, float endTime)
    {
        if (!IsFragmentable) return this;

        var startValue = ValueAtTime(startTime);
        var endValue = ValueAtTime(endTime);
        return new ColorCommand(Easing, startTime, endTime, startValue, endValue);
    }
}