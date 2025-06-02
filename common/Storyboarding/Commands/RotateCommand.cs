namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public record RotateCommand(OsbEasing easing,
    float startTime,
    float endTime,
    CommandDecimal startValue,
    CommandDecimal endValue) : Command<CommandDecimal>("R", easing, startTime, endTime, startValue, endValue)
{
    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToRotation(StartValue);

    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToRotation(EndValue);

    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}