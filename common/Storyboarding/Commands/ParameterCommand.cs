using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public class ParameterCommand(double startTime, double endTime, CommandParameter value) : Command<CommandParameter>("P", 0, startTime, endTime, value, value)
{
    public override bool MaintainValue => StartTime == EndTime;
    public override bool ExportEndValue => false;

    public override CommandParameter ValueAtProgress(double progress) => StartValue;
    public override CommandParameter Midpoint(Command<CommandParameter> endCommand, double progress) => StartValue;

    public override IFragmentableCommand GetFragment(double startTime, double endTime)
    {
        var value = ValueAtTime(startTime);
        return new ParameterCommand(startTime, endTime, value);
    }
}