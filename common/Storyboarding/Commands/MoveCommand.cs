using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public class MoveCommand(OsbEasing easing, double startTime, double endTime, CommandPosition startValue, CommandPosition endValue) : Command<CommandPosition>("M", easing, startTime, endTime, startValue, endValue)
{
    public override CommandPosition GetTransformedStartValue(StoryboardTransform transform) => transform.ApplyToPosition(StartValue);
    public override CommandPosition GetTransformedEndValue(StoryboardTransform transform) => transform.ApplyToPosition(EndValue);

    public override CommandPosition ValueAtProgress(double progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandPosition Midpoint(Command<CommandPosition> endCommand, double progress) => new(StartValue.X + (endCommand.EndValue.X - StartValue.X) * progress, StartValue.Y + (endCommand.EndValue.Y - StartValue.Y) * progress);

    public override IFragmentableCommand GetFragment(double startTime, double endTime)
    {
        if (IsFragmentable)
        {
            var startValue = ValueAtTime(startTime);
            var endValue = ValueAtTime(endTime);
            return new MoveCommand(Easing, startTime, endTime, startValue, endValue);
        }
        return this;
    }
}
public class MoveXCommand(OsbEasing easing, double startTime, double endTime, CommandDecimal startValue, CommandDecimal endValue) : Command<CommandDecimal>("MX", easing, startTime, endTime, startValue, endValue)
{
    public override CommandDecimal GetTransformedStartValue(StoryboardTransform transform) => transform.ApplyToPositionX(StartValue);
    public override CommandDecimal GetTransformedEndValue(StoryboardTransform transform) => transform.ApplyToPositionX(EndValue);

    public override CommandDecimal ValueAtProgress(double progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandDecimal Midpoint(Command<CommandDecimal> endCommand, double progress) => StartValue + (endCommand.EndValue - StartValue) * progress;

    public override IFragmentableCommand GetFragment(double startTime, double endTime)
    {
        if (IsFragmentable)
        {
            var startValue = ValueAtTime(startTime);
            var endValue = ValueAtTime(endTime);
            return new MoveXCommand(Easing, startTime, endTime, startValue, endValue);
        }
        return this;
    }
}
public class MoveYCommand(OsbEasing easing, double startTime, double endTime, CommandDecimal startValue, CommandDecimal endValue) : Command<CommandDecimal>("MY", easing, startTime, endTime, startValue, endValue)
{
    public override CommandDecimal GetTransformedStartValue(StoryboardTransform transform) => transform.ApplyToPositionY(StartValue);
    public override CommandDecimal GetTransformedEndValue(StoryboardTransform transform) => transform.ApplyToPositionY(EndValue);

    public override CommandDecimal ValueAtProgress(double progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandDecimal Midpoint(Command<CommandDecimal> endCommand, double progress) => StartValue + (endCommand.EndValue - StartValue) * progress;

    public override IFragmentableCommand GetFragment(double startTime, double endTime)
    {
        if (IsFragmentable)
        {
            var startValue = ValueAtTime(startTime);
            var endValue = ValueAtTime(endTime);
            return new MoveYCommand(Easing, startTime, endTime, startValue, endValue);
        }
        return this;
    }
}