namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using Animations;
using BrewLib.Util;
using CommandValues;
using Display;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

#pragma warning disable CS1591
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

    public OsbEasing Easing { get; }
    protected virtual bool MaintainValue => true;
    protected virtual bool ExportEndValue => true;

    public void Offset(float offset)
    {
        StartTime += offset;
        EndTime += offset;
    }

    public CommandResult<TValue> AsResult(float timeOffset) => new(this, timeOffset);

    public float StartTime { get; private set; }
    public float EndTime { get; private set; }
    public TValue StartValue { get; }
    public TValue EndValue { get; }

    public TValue ValueAtTime(float time)
    {
        if (time < StartTime) return MaintainValue ? ValueAtProgress(0) : default;
        if (EndTime < time) return MaintainValue ? ValueAtProgress(1) : default;

        var duration = EndTime - StartTime;
        return ValueAtProgress(duration > 0 ? Easing.Ease((time - StartTime) / duration) : 0);
    }

    public int CompareTo(ICommand other)
    {
        var result = float.Round(StartTime).CompareTo(float.Round(other.StartTime));
        if (result == 0) result = float.Round(EndTime).CompareTo(float.Round(other.EndTime));

        if (other is ITypedCommand<TValue> value && result == 0)
        {
            result = EqualityComparer<TValue>.Default.Equals(StartValue, value.StartValue) ? 0 : 1;
            if (result == 0) result = EqualityComparer<TValue>.Default.Equals(EndValue, value.EndValue) ? 0 : 1;
        }

        return result;
    }

    public override int GetHashCode() => HashCode.Combine(Identifier, StartTime, EndTime, StartValue, EndValue);

    public virtual void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        StoryboardTransform transform,
        int indentation)
    {
        for (var i = 0; i < indentation; ++i) writer.Write(' ');

        using var str = ToOsbString(exportSettings, transform);
        writer.WriteLine(str.AsReadOnlySpan());
    }

    protected virtual TValue GetTransformedStartValue(StoryboardTransform transform) => StartValue;
    protected virtual TValue GetTransformedEndValue(StoryboardTransform transform) => EndValue;
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

        var excludeEnd = startTimeString.AsReadOnlySpan().SequenceEqual(endTimeString.AsReadOnlySpan());

        var result = TempList.Create<char>();
        result.AddRange(Identifier.AsSpan());
        result.Add(',');

        using (var easingChars = ((int)Easing).ToCharArray(provider: exportSettings.NumberFormat))
            result.AddRange(easingChars.AsReadOnlySpan());

        result.Add(',');
        result.AddRange(startTimeString.AsReadOnlySpan());

        result.Add(',');
        if (!excludeEnd) result.AddRange(endTimeString.AsReadOnlySpan());

        result.Add(',');
        result.AddRange(startValueString.AsReadOnlySpan());

        if (!startValueString.AsReadOnlySpan().SequenceEqual(endValueString.AsReadOnlySpan()))
        {
            result.Add(',');
            result.AddRange(endValueString.AsReadOnlySpan());
        }

        return result;
    }

    public override string ToString()
    {
        using var str = ToOsbString(ExportSettings.Default, default);
        return str.AsReadOnlySpan().ToString();
    }
}