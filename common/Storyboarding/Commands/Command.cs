namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.IO;
using System.Runtime.CompilerServices;
using BrewLib.Util;
using StorybrewCommon.Animations;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

/// <summary> A command that can be given to an <see cref="OsbSprite"/> to change its properties over time. </summary>
/// <typeparam name="TValue"> The type of value that this command changes over time. </typeparam>
public abstract record Command<TValue> : IComparable<Command<TValue>>, ICommand, IOffsetable
    where TValue : struct, ICommandValue<TValue>
{
    /// <summary> The end value of the command. </summary>
    public readonly TValue EndValue;

    readonly byte flags;

    /// <summary> The start value of the command. </summary>
    public readonly TValue StartValue;

    internal float endTime, startTime;

    private protected Command(OsbEasing easing,
        float startTime,
        float endTime,
        TValue startValue,
        TValue endValue,
        bool maintainValue = true)
    {
        this.startTime = startTime;
        this.endTime = startTime > endTime ? startTime : endTime;

        StartValue = startValue;
        EndValue = endValue;

        flags = (byte)easing;
        if (maintainValue) flags |= 0x80;
    }

    bool MaintainValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (flags & 0x80) != 0;
    }

    /// <summary> The easing function used to interpolate between the start and end times. </summary>
    public OsbEasing Easing
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (OsbEasing)(flags & 0x7F);
    }

    private protected abstract string Identifier { get; }
    private protected virtual bool ExportEndValue => true;

    /// <inheritdoc/>
    public virtual bool IsFragmentableAt(float time) => Easing is OsbEasing.None;

    /// <inheritdoc/>
    public float StartTime => startTime;

    /// <inheritdoc/>
    public float EndTime => endTime;

    /// <inheritdoc/>
    public int CompareTo(ICommand other)
    {
        var result = startTime - other.StartTime;
        if (result != 0) return Math.Sign(result);

        result = endTime - other.EndTime;
        return result != 0 ? Math.Sign(result) :
            other is not Command<TValue> typedOther ? 1 :
            StartValue.Equals(typedOther.StartValue) && EndValue.Equals(typedOther.EndValue) ? 0 : 1;
    }

    void ICommand.WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        scoped ref readonly StoryboardTransform transform,
        int indentation)
    {
        for (var i = 0; i < indentation; ++i) writer.Write(' ');

        using var str = ToOsbString(exportSettings, in transform);
        writer.WriteLine(str.AsReadOnlySpan());
    }

    /// <inheritdoc/>
    public int CompareTo(Command<TValue> other)
    {
        var result = startTime - other.startTime;
        if (result != 0) return Math.Sign(result);

        result = endTime - other.endTime;
        return result != 0 ? Math.Sign(result) :
            StartValue.Equals(other.StartValue) && EndValue.Equals(other.EndValue) ? 0 : 1;
    }

    /// <summary> Offsets the start and end times of the command by the given value. </summary>
    public void Offset(float offset)
    {
        startTime += offset;
        endTime += offset;
    }

    /// <summary> Gets the value of the command at the given time. </summary>
    public TValue ValueAtTime(float time)
    {
        var startT = startTime;
        if (time < startT) return MaintainValue ? StartValue : default;

        var endT = endTime;
        if (endT < time) return MaintainValue ? EndValue : default;

        var duration = endT - startT;
        return StartValue + (EndValue - StartValue) * (duration > 0 ? Easing.Ease((time - startT) / duration) : 0);
    }

    /// <summary> Gets the transformed start value of the command. </summary>
    /// <param name="transform"> The transform to apply to the command's start value. </param>
    protected virtual TValue GetTransformedStartValue(StoryboardTransform transform) => StartValue;

    /// <summary> Gets the transformed end value of the command. </summary>
    /// <param name="transform"> The transform to apply to the command's end value. </param>
    protected virtual TValue GetTransformedEndValue(StoryboardTransform transform) => EndValue;

    TempList<char> ToOsbString(ExportSettings exportSettings, scoped ref readonly StoryboardTransform transform)
    {
        using var startTimeString =
            (exportSettings.UseFloatForTime ? startTime : (int)float.Round(startTime)).ToCharArray(
                provider: exportSettings.NumberFormat);

        using var endTimeString =
            (exportSettings.UseFloatForTime ? endTime : (int)float.Round(endTime)).ToCharArray(
                provider: exportSettings.NumberFormat);

        var tranformedStartValue = GetTransformedStartValue(transform);
        using var startValueString = tranformedStartValue.ToOsbString(exportSettings);
        using var endValueString = (ExportEndValue ? GetTransformedEndValue(transform) : tranformedStartValue)
            .ToOsbString(exportSettings);

        var result = StringHelper.Interpolate(exportSettings.NumberFormat,
            $"{Identifier},{(int)Easing},{startTimeString.AsReadOnlySpan()},");

        if (!startTimeString.AsReadOnlySpan().SequenceEqual(endTimeString.AsReadOnlySpan()))
            result.AddRange(endTimeString.AsReadOnlySpan());

        result.Append($",{startValueString.AsReadOnlySpan()}");
        if (!startValueString.AsReadOnlySpan().SequenceEqual(endValueString.AsReadOnlySpan()))
            result.Append($",{endValueString.AsReadOnlySpan()}");

        return result;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        using var str = ToOsbString(ExportSettings.Default, in StoryboardTransform.Identity);
        return str.AsReadOnlySpan().ToString();
    }
}