namespace StorybrewCommon.Storyboarding.Commands;

using System;
using CommandValues;

#pragma warning disable CS1591
public record ScaleCommand(OsbEasing easing,
    float startTime,
    float endTime,
    CommandDecimal startValue,
    CommandDecimal endValue) : Command<CommandDecimal>("S", easing, startTime, endTime, startValue, endValue)
{
    protected override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToScale(StartValue);

    protected override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToScale(EndValue);

    public override CommandDecimal ValueAtProgress(float progress)
        => Math.Max(0, StartValue + (EndValue - StartValue) * progress);
}

public record VScaleCommand(OsbEasing easing, float startTime, float endTime, CommandScale startValue, CommandScale endValue)
    : Command<CommandScale>("V", easing, startTime, endTime, startValue, endValue)
{
    protected override CommandScale GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToScale(StartValue);

    protected override CommandScale GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToScale(EndValue);

    public override CommandScale ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
}