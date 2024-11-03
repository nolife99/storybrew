using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public class RotateCommand(OsbEasing easing, double startTime, double endTime, CommandDecimal startValue, CommandDecimal endValue) : Command<CommandDecimal>("R", easing, startTime, endTime, startValue, endValue)
{
    public override CommandDecimal GetTransformedStartValue(StoryboardTransform transform) => transform.ApplyToRotation(StartValue);
    public override CommandDecimal GetTransformedEndValue(StoryboardTransform transform) => transform.ApplyToRotation(EndValue);

    public override CommandDecimal ValueAtProgress(double progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandDecimal Midpoint(Command<CommandDecimal> endCommand, double progress) => StartValue + (endCommand.EndValue - StartValue) * progress;

    public override IFragmentableCommand GetFragment(double startTime, double endTime)
    {
        if (IsFragmentable)
        {
            var startValue = ValueAtTime(startTime);
            var endValue = ValueAtTime(endTime);
            return new RotateCommand(Easing, startTime, endTime, startValue, endValue);
        }
        return this;
    }
}