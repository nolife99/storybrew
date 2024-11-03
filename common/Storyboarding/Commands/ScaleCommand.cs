using System;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public class ScaleCommand(OsbEasing easing, double startTime, double endTime, CommandDecimal startValue, CommandDecimal endValue) : Command<CommandDecimal>("S", easing, startTime, endTime, startValue, endValue)
{
    public override CommandDecimal GetTransformedStartValue(StoryboardTransform transform) => transform.ApplyToScale(StartValue);
    public override CommandDecimal GetTransformedEndValue(StoryboardTransform transform) => transform.ApplyToScale(EndValue);

    // Scale commands can't return a negative size
    public override CommandDecimal ValueAtProgress(double progress) => Math.Max(0, StartValue + (EndValue - StartValue) * progress);
    public override CommandDecimal Midpoint(Command<CommandDecimal> endCommand, double progress) => StartValue + (endCommand.EndValue - StartValue) * progress;
    public override IFragmentableCommand GetFragment(double startTime, double endTime)
    {
        if (IsFragmentable && StartValue >= 0 && EndValue >= 0)
        {
            var startValue = ValueAtTime(startTime);
            var endValue = ValueAtTime(endTime);
            return new ScaleCommand(Easing, startTime, endTime, startValue, endValue);
        }
        return this;
    }
}
public class VScaleCommand(OsbEasing easing, double startTime, double endTime, CommandScale startValue, CommandScale endValue) : Command<CommandScale>("V", easing, startTime, endTime, startValue, endValue)
{
    public override CommandScale GetTransformedStartValue(StoryboardTransform transform) => transform.ApplyToScale(StartValue);
    public override CommandScale GetTransformedEndValue(StoryboardTransform transform) => transform.ApplyToScale(EndValue);

    public override CommandScale ValueAtProgress(double progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandScale Midpoint(Command<CommandScale> endCommand, double progress) => new(
        StartValue.X + (endCommand.EndValue.X - StartValue.X) * progress, StartValue.Y + (endCommand.EndValue.Y - StartValue.Y) * progress);

    public override IFragmentableCommand GetFragment(double startTime, double endTime)
    {
        if (IsFragmentable)
        {
            var startValue = ValueAtTime(startTime);
            var endValue = ValueAtTime(endTime);
            return new VScaleCommand(Easing, startTime, endTime, startValue, endValue);
        }
        return this;
    }
}