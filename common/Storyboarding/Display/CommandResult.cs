namespace StorybrewCommon.Storyboarding.Display;

using System.Runtime.CompilerServices;
using CommandValues;

/// <summary>
/// The absolute result of a command that can be given to an <see cref="OsbSprite"/> to change its properties over
/// time.
/// </summary>
/// <typeparam name="TValue"> The type of value that this command changes over time. </typeparam>
public readonly struct CommandResult<TValue> where TValue : struct, ICommandValue<TValue>
{
    internal readonly CommandChannel<TValue> Channel;
    internal readonly int Index;

    readonly float TimeOffset;

    public float StartTime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Channel.StartTimeAt(Index) + TimeOffset;
    }

    public float EndTime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Channel.EndTimeAt(Index) + TimeOffset;
    }

    public TValue StartValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Channel.StartValueAt(Index);
    }

    public TValue EndValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Channel.EndValueAt(Index);
    }

    public OsbEasing Easing
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Channel.EasingAt(Index);
    }

    public CommandKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Channel.KindAt(Index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CommandResult(CommandChannel<TValue> channel, int index, float timeOffset = 0)
    {
        Channel = channel;
        Index = index;
        TimeOffset = timeOffset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandResult<TValue> WithoutOffset() => new(Channel, Index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBefore(CommandResult<TValue> other)
        => StartTime < other.StartTime || StartTime == other.StartTime && EndTime < other.EndTime;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue ValueAtTime(float time) => Channel.ValueAtIndex(Index, time - TimeOffset);
}
