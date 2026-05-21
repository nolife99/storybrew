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

    /// <inheritdoc/>
    protected override CommandPosition GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPosition(StartValue);

    /// <inheritdoc/>
    protected override CommandPosition GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToPosition(EndValue);
}

public sealed record MoveXCommand : Command<CommandDecimal>
{
    public MoveXCommand(OsbEasing easing,
        float startTime,
        float endTime,
        CommandDecimal startValue,
        CommandDecimal endValue) : base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "MX";

    /// <inheritdoc/>
    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPositionX(StartValue);

    /// <inheritdoc/>
    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToPositionX(EndValue);
}

public sealed record MoveYCommand : Command<CommandDecimal>
{
    public MoveYCommand(OsbEasing easing,
        float startTime,
        float endTime,
        CommandDecimal startValue,
        CommandDecimal endValue) : base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "MY";

    /// <inheritdoc/>
    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToPositionY(StartValue);

    /// <inheritdoc/>
    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToPositionY(EndValue);
}