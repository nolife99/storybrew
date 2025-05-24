namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Animations;
using BrewLib.Util;
using CommandValues;
using Display;

#pragma warning disable CS1591
public abstract record Command<TValue> : ITypedCommand<TValue>, IFragmentableCommand, IOffsetable
    where TValue : struct, CommandValue
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
    }

    public OsbEasing Easing { get; set; }
    protected virtual bool MaintainValue => true;
    protected virtual bool ExportEndValue => true;
    protected bool IsFragmentable => StartTime == EndTime || Easing is OsbEasing.None;
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

        var str = ToOsbString(exportSettings, transform);
        writer.WriteLine(str);
        StringHelper.StringBuilderPool.Release(str);
    }

    public virtual TValue GetTransformedStartValue(StoryboardTransform transform) => StartValue;
    public virtual TValue GetTransformedEndValue(StoryboardTransform transform) => EndValue;
    public abstract TValue ValueAtProgress(float progress);

    public StringBuilder ToOsbString(ExportSettings exportSettings, StoryboardTransform transform)
    {
        var startTimeString =
            (exportSettings.UseFloatForTime ? StartTime : (int)StartTime).ToString(exportSettings.NumberFormat);

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