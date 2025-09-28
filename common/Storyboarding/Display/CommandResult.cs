namespace StorybrewCommon.Storyboarding.Display;

using System.Runtime.CompilerServices;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;

/// <summary>
/// The absolute result of a command that can be given to an <see cref="OsbSprite"/> to change its properties over
/// time.
/// </summary>
/// <typeparam name="TValue"> The type of value that this command changes over time. </typeparam>
/// <seealso cref="Command{TValue}"/>
public readonly struct CommandResult<TValue> where TValue : struct, ICommandValue<TValue>
{
    internal readonly Command<TValue> Command;
    readonly float timeOffset;

    public readonly float StartTime, EndTime;

    public TValue StartValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Command.StartValue;
    }

    public TValue EndValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Command.EndValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CommandResult(Command<TValue> command, float timeOffset = 0)
    {
        Command = command;
        this.timeOffset = timeOffset;

        StartTime = command.startTime + timeOffset;
        EndTime = command.endTime + timeOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBefore(CommandResult<TValue> other)
        => StartTime < other.StartTime || StartTime == other.StartTime && EndTime < other.EndTime;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue ValueAtTime(float time) => Command.ValueAtTime(time - timeOffset);
}