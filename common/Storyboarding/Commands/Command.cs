namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using BrewLib.Util;
using StorybrewCommon.Animations;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding.Display;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

/// <summary> A command that can be given to an <see cref="OsbSprite"/> to change its properties over time. </summary>
/// <typeparam name="TValue"> The type of value that this command changes over time. </typeparam>
public abstract record Command<TValue> : ITypedCommand<TValue>, IOffsetable where TValue : struct, ICommandValue
{
    private protected Command(OsbEasing easing, float startTime, float endTime, TValue startValue, TValue endValue)
    {
        Easing = easing;
        StartTime = startTime;
        EndTime = endTime;
        StartValue = startValue;
        EndValue = endValue;

        if (startTime > endTime) EndTime = startTime;
    }

    private protected abstract string Identifier { get; }

    /// <summary> The easing function used to interpolate between the start and end times. </summary>
    public OsbEasing Easing { get; }

    private protected virtual bool MaintainValue => true;
    private protected virtual bool ExportEndValue => true;

    /// <summary> Offsets the start and end times of the command by the given value. </summary>
    public void Offset(float offset)
    {
        StartTime += offset;
        EndTime += offset;
    }

    /// <inheritdoc/>
    public virtual bool IsFragmentableAt(float time) => Easing == OsbEasing.None;

    /// <inheritdoc/>
    public CommandResult<TValue> AsResult(float timeOffset) => new(this, timeOffset);

    /// <inheritdoc/>
    public float StartTime { get; private set; }

    /// <inheritdoc/>
    public float EndTime { get; private set; }

    /// <inheritdoc/>
    public TValue StartValue { get; }

    /// <inheritdoc/>
    public TValue EndValue { get; }

    /// <inheritdoc/>
    public TValue ValueAtTime(float time)
    {
        if (time < StartTime) return MaintainValue ? ValueAtProgress(0) : default;
        if (EndTime < time) return MaintainValue ? ValueAtProgress(1) : default;

        var duration = EndTime - StartTime;
        return ValueAtProgress(duration > 0 ? Easing.Ease((time - StartTime) / duration) : 0);
    }

    /// <inheritdoc/>
    public int CompareTo(ICommand other)
    {
        var result = StartTime.CompareTo(other.StartTime);
        if (result != 0) return result;

        result = EndTime.CompareTo(other.EndTime);
        if (result != 0) return result;

        if (other is not ITypedCommand<TValue> typedOther) return 1;

        return EqualityComparer<TValue>.Default.Equals(StartValue, typedOther.StartValue) &&
            EqualityComparer<TValue>.Default.Equals(EndValue, typedOther.EndValue) ?
                0 :
                1;
    }

    /// <inheritdoc/>
    public virtual void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        StoryboardTransform transform,
        int indentation)
    {
        for (var i = 0; i < indentation; ++i) writer.Write(' ');

        using var str = ToOsbString(exportSettings, transform);
        writer.WriteLine(str.AsReadOnlySpan());
    }

    /// <summary> Gets the transformed start value of the command. </summary>
    /// <param name="transform"> The transform to apply to the command's start value. </param>
    protected virtual TValue GetTransformedStartValue(StoryboardTransform transform) => StartValue;

    /// <summary> Gets the transformed end value of the command. </summary>
    /// <param name="transform"> The transform to apply to the command's end value. </param>
    protected virtual TValue GetTransformedEndValue(StoryboardTransform transform) => EndValue;

    /// <summary> Gets the value of the command at the given progress. </summary>
    public abstract TValue ValueAtProgress(float progress);

    TempList<char> ToOsbString(ExportSettings exportSettings, StoryboardTransform transform)
    {
        using var startTimeString =
            (exportSettings.UseFloatForTime ? StartTime : (int)float.Round(StartTime)).ToCharArray(
                provider: exportSettings.NumberFormat);

        using var endTimeString =
            (exportSettings.UseFloatForTime ? EndTime : (int)float.Round(EndTime)).ToCharArray(
                provider: exportSettings.NumberFormat);

        var tranformedStartValue = GetTransformedStartValue(transform);
        using var startValueString = tranformedStartValue.ToOsbString(exportSettings);
        using var endValueString =
            (ExportEndValue ? GetTransformedEndValue(transform) : tranformedStartValue).ToOsbString(exportSettings);

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
        using var str = ToOsbString(ExportSettings.Default, StoryboardTransform.Identity);
        return str.AsReadOnlySpan().ToString();
    }
}