namespace StorybrewCommon.Storyboarding.Display;

using Commands;
using CommandValues;

public readonly struct CommandResult<TValue> where TValue : struct, ICommandValue
{
    readonly Command<TValue> command;
    readonly float timeOffset;

    public float StartTime { get; }
    public float EndTime { get; }

    public TValue StartValue => command.StartValue;
    public TValue EndValue => command.EndValue;

    internal CommandResult(Command<TValue> command, float timeOffset)
    {
        this.command = command;
        this.timeOffset = timeOffset;

        StartTime = command.StartTime + timeOffset;
        EndTime = command.EndTime + timeOffset;
    }

    public bool IsBefore(CommandResult<TValue> other)
        => StartTime < other.StartTime || StartTime == other.StartTime && EndTime < other.EndTime;

    public TValue ValueAtTime(float time) => command.ValueAtTime(time - timeOffset);
}