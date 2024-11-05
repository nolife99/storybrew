using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public class FadeCommand(OsbEasing easing, float startTime, float endTime, CommandDecimal startValue, CommandDecimal endValue) : Command<CommandDecimal>("F", easing, startTime, endTime, startValue, endValue)
{
    public override CommandDecimal ValueAtProgress(float progress) => StartValue + (EndValue - StartValue) * progress;
    public override CommandDecimal Midpoint(Command<CommandDecimal> endCommand, float progress) => StartValue + (endCommand.EndValue - StartValue) * progress;

    public override IFragmentableCommand GetFragment(float startTime, float endTime)
    {
        if (IsFragmentable)
        {
            var startValue = ValueAtTime(startTime);
            var endValue = ValueAtTime(endTime);
            return new FadeCommand(Easing, startTime, endTime, startValue, endValue);
        }
        return this;
    }
}