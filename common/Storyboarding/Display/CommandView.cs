namespace StorybrewCommon.Storyboarding.Display;

using System.Runtime.CompilerServices;
using Animations;
using CommandValues;

internal readonly struct CommandView<TValue> where TValue : struct, ICommandValue<TValue>
{
    public readonly CommandKind Kind;
    public readonly float StartTime, EndTime;
    public readonly float BaseStartTime, BaseEndTime, TimeOffset;
    public readonly OsbEasing Easing;
    public readonly TValue StartValue, EndValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CommandView(CommandKind kind,
        float baseStartTime,
        float baseEndTime,
        float timeOffset,
        OsbEasing easing,
        TValue startValue,
        TValue endValue)
    {
        Kind = kind;
        BaseStartTime = baseStartTime;
        BaseEndTime = baseEndTime;
        TimeOffset = timeOffset;
        StartTime = baseStartTime + timeOffset;
        EndTime = baseEndTime + timeOffset;
        Easing = easing;
        StartValue = startValue;
        EndValue = endValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsFragmentableAt(float time) => Easing is OsbEasing.None;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue ValueAtTime(float time)
    {
        var localTime = time - TimeOffset;
        var duration = BaseEndTime - BaseStartTime;
        return StartValue + (EndValue - StartValue) *
            (duration > 0 ? Easing.Ease((localTime - BaseStartTime) / duration) : 0);
    }
}
