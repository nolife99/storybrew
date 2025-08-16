namespace StorybrewCommon.Storyboarding.Commands;

using StorybrewCommon.Storyboarding.CommandValues;

#pragma warning disable CS1591
public sealed record ParameterCommand : Command<CommandParameter>
{
    public ParameterCommand(float startTime, float endTime, CommandParameter value) : base(0,
        startTime,
        endTime,
        value,
        value,
        startTime == endTime) { }

    private protected override string Identifier => "P";
    private protected override bool ExportEndValue => false;
}