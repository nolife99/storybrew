namespace StorybrewCommon.Storyboarding.Commands;

using StorybrewCommon.Storyboarding.CommandValues;

#pragma warning disable CS1591
public sealed record RotateCommand : Command<CommandDecimal>
{
    public RotateCommand(OsbEasing easing,
        float startTime,
        float endTime,
        CommandDecimal startValue,
        CommandDecimal endValue) : base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "R";

    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToRotation(StartValue);

    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToRotation(EndValue);

    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}