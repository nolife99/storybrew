namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Animations;
using CommandValues;

#pragma warning disable CS1591
public abstract class Command<TValue>(string identifier,
    OsbEasing easing,
    float startTime,
    float endTime,
    TValue startValue,
    TValue endValue) : ITypedCommand<TValue>, IFragmentableCommand, IOffsetable where TValue : CommandValue
{
    public string Identifier { get; set; } = identifier;
    public OsbEasing Easing { get; set; } = easing;
    public float Duration => EndTime - StartTime;
    public virtual bool MaintainValue => true;
    public virtual bool ExportEndValue => true;

    public bool IsFragmentable => StartTime == EndTime || Easing is OsbEasing.None;
    public abstract IFragmentableCommand GetFragment(float startTime, float endTime);

    public IEnumerable<int> GetNonFragmentableTimes()
    {
        if (!IsFragmentable)
            for (var i = 0; i < EndTime - StartTime - 1; ++i)
                yield return (int)(StartTime + 1 + i);
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
        var progress = duration > 0 ? Easing.Ease((time - StartTime) / duration) : 0;
        return ValueAtProgress((float)progress);
    }

    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);
    public override int GetHashCode() => HashCode.Combine(Identifier, StartTime, EndTime, StartValue, EndValue);

    public virtual void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation)
    {
        Span<char> indent = stackalloc char[indentation];
        indent.Fill(' ');

        writer.Write(indent);
        writer.WriteLine(ToOsbString(exportSettings, transform));
    }

    public virtual TValue GetTransformedStartValue(StoryboardTransform transform) => StartValue;
    public virtual TValue GetTransformedEndValue(StoryboardTransform transform) => EndValue;

    public abstract TValue ValueAtProgress(float progress);
    public abstract TValue Midpoint(Command<TValue> endCommand, float progress);

    public override bool Equals(object obj) => obj is Command<TValue> other && Equals(other);
    public bool Equals(Command<TValue> obj) => Identifier == obj.Identifier && Easing == obj.Easing &&
        StartTime == obj.StartTime && EndTime == obj.EndTime && StartValue.Equals(obj.StartValue) &&
        EndValue.Equals(obj.EndValue);

    public StringBuilder ToOsbString(ExportSettings exportSettings, StoryboardTransform transform)
    {
        var startTimeString = (exportSettings.UseFloatForTime ? StartTime : (int)StartTime).ToString(exportSettings.NumberFormat);
        var endTimeString = (exportSettings.UseFloatForTime ? EndTime : (int)EndTime).ToString(exportSettings.NumberFormat);

        var tranformedStartValue = transform is not null ? GetTransformedStartValue(transform) : StartValue;
        var tranformedEndValue = transform is not null ? GetTransformedEndValue(transform) : EndValue;
        var startValueString = tranformedStartValue.ToOsbString(exportSettings);
        var endValueString = (ExportEndValue ? tranformedEndValue : tranformedStartValue).ToOsbString(exportSettings);

        StringBuilder result = new();
        if (startTimeString == endTimeString) endTimeString = "";

        result.AppendJoin(',', Identifier, ((int)Easing).ToString(exportSettings.NumberFormat), startTimeString, endTimeString,
            startValueString);

        if (startValueString == endValueString) return result;

        result.Append(',');
        return result.Append(endValueString);
    }
    public override string ToString() => ToOsbString(ExportSettings.Default, null).ToString();
}