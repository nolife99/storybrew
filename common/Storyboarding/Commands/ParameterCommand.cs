namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public record ParameterCommand : Command<CommandParameter>
{
    internal ParameterCommand(float startTime, float endTime, CommandParameter value) : base("P",
        0,
        startTime,
        endTime,
        value,
        value) { }

    protected override bool MaintainValue => StartTime == EndTime;
    protected override bool ExportEndValue => false;

    public override CommandParameter ValueAtProgress(float progress) => StartValue;

    public override IFragmentableCommand GetFragment(float startTime, float endTime)
        => new ParameterCommand(startTime, endTime, ValueAtTime(startTime));
}