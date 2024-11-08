namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public class RotateCommand(
    OsbEasing easing, float startTime, float endTime, CommandDecimal startValue, CommandDecimal endValue)
    : Command<CommandDecimal>("R", easing, startTime, endTime, startValue, endValue)
{
    public override CommandDecimal GetTransformedStartValue(StoryboardTransform transform)
        => transform.ApplyToRotation(StartValue);

    public override CommandDecimal GetTransformedEndValue(StoryboardTransform transform)
        => transform.ApplyToRotation(EndValue);

    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;

    public override CommandDecimal Midpoint(Command<CommandDecimal> endCommand, float progress)
        => StartValue + (endCommand.EndValue - StartValue) * progress;

    public override IFragmentableCommand GetFragment(float startTime, float endTime)
    {
        if (!IsFragmentable) return this;
        var startValue = ValueAtTime(startTime);
        var endValue = ValueAtTime(endTime);
        return new RotateCommand(Easing, startTime, endTime, startValue, endValue);
    }
}