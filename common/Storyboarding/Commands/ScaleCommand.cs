namespace StorybrewCommon.Storyboarding.Commands;

using StorybrewCommon.Storyboarding.CommandValues;

#pragma warning disable CS1591
public sealed record ScaleCommand : Command<CommandDecimal>
{
    public ScaleCommand(OsbEasing easing, float startTime, float endTime, CommandDecimal startValue, CommandDecimal endValue)
        : base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "S";

    public override bool IsFragmentableAt(float time) => base.IsFragmentableAt(time) && StartValue >= 0 && EndValue >= 0;

    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToScale(StartValue);

    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToScale(EndValue);

    public override CommandDecimal ValueAtProgress(float progress)
        => float.Max(0, StartValue + (EndValue - StartValue) * progress);
}

public sealed record VScaleCommand : Command<CommandScale>
{
    internal VScaleCommand(OsbEasing easing, float startTime, float endTime, CommandScale startValue, CommandScale endValue)
        : base(easing, startTime, endTime, startValue, endValue) { }

    private protected override string Identifier => "V";

    protected override CommandScale GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToScale(StartValue);

    protected override CommandScale GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToScale(EndValue);

    public override CommandScale ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}