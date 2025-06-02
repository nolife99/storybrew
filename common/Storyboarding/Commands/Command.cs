namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.IO;
using Animations;
using BrewLib.Util;
using CommandValues;
using Display;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals.Safe;

#pragma warning disable CS1591
public abstract record Command<TValue> : ITypedCommand<TValue>, IOffsetable where TValue : struct, ICommandValue
{
    readonly string identifier;

    protected internal Command(string identifier,
        OsbEasing easing,
        float startTime,
        float endTime,
        TValue startValue,
        TValue endValue)
    {
        this.identifier = identifier;
        Easing = easing;
        StartTime = startTime;
        EndTime = endTime;
        StartValue = startValue;
        EndValue = endValue;

        if (startTime > endTime) EndTime = startTime;
    }

    public OsbEasing Easing { get; set; }
    protected virtual bool MaintainValue => true;
    protected virtual bool ExportEndValue => true;

    public void Offset(float offset)
    {
        StartTime += offset;
        EndTime += offset;
    }

    public CommandResult<TValue> AsResult(float timeOffset) => new(this, timeOffset);

    public float StartTime { get; set; }
    public float EndTime { get; set; }
    public TValue StartValue { get; set; }
    public TValue EndValue { get; set; }

    public TValue ValueAtTime(float time)
    {
        if (time < StartTime) return MaintainValue ? ValueAtProgress(0) : default;
        if (EndTime < time) return MaintainValue ? ValueAtProgress(1) : default;

        var duration = EndTime - StartTime;
        return ValueAtProgress(duration > 0 ? Easing.Ease((time - StartTime) / duration) : 0);
    }

    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);
    public override int GetHashCode() => HashCode.Combine(identifier, StartTime, EndTime, StartValue, EndValue);

    public virtual void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        StoryboardTransform transform,
        int indentation)
    {
        Span<char> indent = stackalloc char[indentation];
        indent.Fill(' ');

        writer.Write(indent);

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

        var excludeEnd = startTimeString.AsReadOnlySpan().Equals(endTimeString.AsReadOnlySpan(), StringComparison.Ordinal);

        var result = TempList<char>.Create();
        result.AddRange(identifier.AsSpan());
        result.Add(',');

        using (var easingChars = ((int)Easing).ToCharArray(provider: exportSettings.NumberFormat))
            result.AddRange(easingChars.AsReadOnlySpan());

        result.Add(',');
        result.AddRange(startTimeString.AsReadOnlySpan());

        result.Add(',');
        if (!excludeEnd) result.AddRange(endTimeString.AsReadOnlySpan());

        result.Add(',');
        result.AddRange(startValueString.AsReadOnlySpan());

        if (!startValueString.AsReadOnlySpan().Equals(endValueString.AsReadOnlySpan(), StringComparison.Ordinal))
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