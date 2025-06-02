namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public record MoveCommand(OsbEasing easing,
    float startTime,
    float endTime,
    CommandPosition startValue,
    CommandPosition endValue) : Command<CommandPosition>("M", easing, startTime, endTime, startValue, endValue)
{
    protected override CommandPosition GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPosition(StartValue);

    protected override CommandPosition GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToPosition(EndValue);

    public override CommandPosition ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}

public record MoveXCommand(OsbEasing easing,
    float startTime,
    float endTime,
    CommandDecimal startValue,
    CommandDecimal endValue) : Command<CommandDecimal>("MX", easing, startTime, endTime, startValue, endValue)
{
    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPositionX(StartValue);

    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToPositionX(EndValue);

    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}

public record MoveYCommand(OsbEasing easing,
    float startTime,
    float endTime,
    CommandDecimal startValue,
    CommandDecimal endValue) : Command<CommandDecimal>("MY", easing, startTime, endTime, startValue, endValue)
{
    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPositionY(StartValue);

    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToPositionY(EndValue);

    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}