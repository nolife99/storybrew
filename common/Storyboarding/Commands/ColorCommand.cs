namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public record ColorCommand(OsbEasing easing, float startTime, float endTime, CommandColor startValue, CommandColor endValue)
    : Command<CommandColor>("C", easing, startTime, endTime, startValue, endValue)
{
    public override CommandColor ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}