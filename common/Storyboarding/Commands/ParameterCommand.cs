namespace StorybrewCommon.Storyboarding.Commands;

using CommandValues;

#pragma warning disable CS1591
public sealed record ParameterCommand : Command<CommandParameter>
{
    public ParameterCommand(float startTime, float endTime, CommandParameter value) : base(0,
        startTime,
        endTime,
        value,
        value) { }

    private protected override string Identifier => "P";

    protected override bool MaintainValue => StartTime == EndTime;
    protected override bool ExportEndValue => false;

    public override CommandParameter ValueAtProgress(float progress) => StartValue;
}