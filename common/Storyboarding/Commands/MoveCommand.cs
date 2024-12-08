namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public record MoveCommand(OsbEasing easing, float startTime, float endTime, CommandPosition startValue, CommandPosition endValue)
    : Command<CommandPosition>("M", easing, startTime, endTime, startValue, endValue)
{
    public override CommandPosition GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPosition(StartValue);
    public override CommandPosition GetTransformedEndValue(StoryboardTransform transform) => transform.ApplyToPosition(EndValue);
    public override CommandPosition ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandPosition Midpoint(Command<CommandPosition> endCommand, float progress) => new(
        StartValue.X + (endCommand.EndValue.X - StartValue.X) * progress,
        StartValue.Y + (endCommand.EndValue.Y - StartValue.Y) * progress);
    public override IFragmentableCommand GetFragment(float startTime, float endTime) => IsFragmentable ?
        new MoveCommand(Easing, startTime, endTime, ValueAtTime(startTime), ValueAtTime(endTime)) :
        this;
}

public record MoveXCommand(OsbEasing easing, float startTime, float endTime, CommandDecimal startValue, CommandDecimal endValue)
    : Command<CommandDecimal>("MX", easing, startTime, endTime, startValue, endValue)
{
    public override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPositionX(StartValue);
    public override CommandDecimal GetTransformedEndValue(StoryboardTransform transform) => transform.ApplyToPositionX(EndValue);
    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandDecimal Midpoint(Command<CommandDecimal> endCommand, float progress)
        => StartValue + (endCommand.EndValue - StartValue) * progress;
    public override IFragmentableCommand GetFragment(float startTime, float endTime) => IsFragmentable ?
        new MoveXCommand(Easing, startTime, endTime, ValueAtTime(startTime), ValueAtTime(endTime)) :
        this;
}

public record MoveYCommand(OsbEasing easing, float startTime, float endTime, CommandDecimal startValue, CommandDecimal endValue)
    : Command<CommandDecimal>("MY", easing, startTime, endTime, startValue, endValue)
{
    public override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPositionY(StartValue);
    public override CommandDecimal GetTransformedEndValue(StoryboardTransform transform) => transform.ApplyToPositionY(EndValue);
    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandDecimal Midpoint(Command<CommandDecimal> endCommand, float progress)
        => StartValue + (endCommand.EndValue - StartValue) * progress;
    public override IFragmentableCommand GetFragment(float startTime, float endTime) => IsFragmentable ?
        new MoveYCommand(Easing, startTime, endTime, ValueAtTime(startTime), ValueAtTime(endTime)) :
        this;
}