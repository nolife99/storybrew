namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public sealed record MoveCommand : Command<CommandPosition>
{
    public MoveCommand(OsbEasing easing,
        float startTime,
        float endTime,
        CommandPosition startValue,
        CommandPosition endValue) : base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "M";

    protected override CommandPosition GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPosition(StartValue);

    protected override CommandPosition GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToPosition(EndValue);

    public override CommandPosition ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}

public sealed record MoveXCommand : Command<CommandDecimal>
{
    public MoveXCommand(OsbEasing easing, float startTime, float endTime, CommandDecimal startValue, CommandDecimal endValue)
        : base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "MX";

    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPositionX(StartValue);

    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToPositionX(EndValue);

    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}

public sealed record MoveYCommand : Command<CommandDecimal>
{
    public MoveYCommand(OsbEasing easing, float startTime, float endTime, CommandDecimal startValue, CommandDecimal endValue)
        : base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "MY";

    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPositionY(StartValue);

    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToPositionY(EndValue);

    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}