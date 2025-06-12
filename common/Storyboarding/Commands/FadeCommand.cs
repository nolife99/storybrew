namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public sealed record FadeCommand : Command<CommandDecimal>
{
    public FadeCommand(OsbEasing easing, float startTime, float endTime, CommandDecimal startValue, CommandDecimal endValue)
        : base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "F";
    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}