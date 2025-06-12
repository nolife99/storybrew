namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public sealed record ColorCommand : Command<CommandColor>
{
    public ColorCommand(OsbEasing easing, float startTime, float endTime, CommandColor startValue, CommandColor endValue) :
        base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "C";
    public override CommandColor ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}