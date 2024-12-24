namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Animations;
using BrewLib.Util;
using CommandValues;

#pragma warning disable CS1591
public abstract record Command<TValue>(string identifier,
    OsbEasing easing,
    float startTime,
    float endTime,
    TValue startValue,
    TValue endValue) : ITypedCommand<TValue>, IFragmentableCommand, IOffsetable where TValue : CommandValue
{
    public OsbEasing Easing { get; set; } = easing;
    protected virtual bool MaintainValue => true;
    protected virtual bool ExportEndValue => true;
    protected bool IsFragmentable => StartTime == EndTime || easing is OsbEasing.None;
    public abstract IFragmentableCommand GetFragment(float startTime, float endTime);
    public IEnumerable<int> GetNonFragmentableTimes()
    {
        if (IsFragmentable) yield break;
        for (var i = 0; i < EndTime - StartTime - 1; ++i) yield return (int)(StartTime + 1 + i);
    }
    public void Offset(float offset)
    {
        StartTime += offset;
        EndTime += offset;
    }
    public float StartTime { get; set; } = startTime;
    public float EndTime { get; set; } = endTime;
    public TValue StartValue { get; set; } = startValue;
    public TValue EndValue { get; set; } = endValue;
    public bool Active => true;
    public int Cost => 1;
    public TValue ValueAtTime(float time)
    {
        if (time < StartTime) return MaintainValue ? ValueAtProgress(0) : default;
        if (EndTime < time) return MaintainValue ? ValueAtProgress(1) : default;

        var duration = EndTime - StartTime;
        var progress = duration > 0 ? easing.Ease((time - StartTime) / duration) : 0;
        return ValueAtProgress(progress);
    }
    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);
    public override int GetHashCode() => HashCode.Combine(identifier, StartTime, EndTime, StartValue, EndValue);
    public virtual void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation)
    {
        Span<char> indent = stackalloc char[indentation];
        indent.Fill(' ');

        writer.Write(indent);

        var str = ToOsbString(exportSettings, transform);
        writer.WriteLine(str);
        StringHelper.StringBuilderPool.Release(str);
    }
    public virtual TValue GetTransformedStartValue(StoryboardTransform transform) => StartValue;
    public virtual TValue GetTransformedEndValue(StoryboardTransform transform) => EndValue;
    public abstract TValue ValueAtProgress(float progress);
    public abstract TValue Midpoint(Command<TValue> endCommand, float progress);
    public StringBuilder ToOsbString(ExportSettings exportSettings, StoryboardTransform transform)
    {
        var startTimeString = (exportSettings.UseFloatForTime ? StartTime : (int)StartTime).ToString(exportSettings.NumberFormat);
        var endTimeString = (exportSettings.UseFloatForTime ? EndTime : (int)EndTime).ToString(exportSettings.NumberFormat);

        var identity = !transform.IsIdentity;

        var tranformedStartValue = identity ? GetTransformedStartValue(transform) : StartValue;
        var tranformedEndValue = identity ? GetTransformedEndValue(transform) : EndValue;
        var startValueString = tranformedStartValue.ToOsbString(exportSettings);
        var endValueString = (ExportEndValue ? tranformedEndValue : tranformedStartValue).ToOsbString(exportSettings);

        var result = StringHelper.StringBuilderPool.Retrieve();
        if (startTimeString.Equals(endTimeString, StringComparison.Ordinal)) endTimeString = "";

        result.AppendJoin(',',
            identifier,
            ((int)Easing).ToString(exportSettings.NumberFormat),
            startTimeString,
            endTimeString,
            startValueString);

        if (startValueString.Equals(endValueString, StringComparison.Ordinal)) return result;

        result.Append(',');
        return result.Append(endValueString);
    }
    public override string ToString()
    {
        var str = ToOsbString(ExportSettings.Default, default);
        var result = str.ToString();
        StringHelper.StringBuilderPool.Release(str);
        return result;
    }
}