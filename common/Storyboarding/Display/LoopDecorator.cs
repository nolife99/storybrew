namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.IO;
using Commands;
using CommandValues;

#pragma warning disable CS1591
public class LoopDecorator<TValue>(ITypedCommand<TValue> command, float startTime, float repeatDuration, int repeats)
    : ITypedCommand<TValue> where TValue : CommandValue
{
    public float Duration => EndTime - StartTime;
    public float RepeatDuration => repeatDuration < 0 ? command.EndTime : repeatDuration;

    public OsbEasing Easing => throw new InvalidOperationException();
    public float StartTime => startTime;
    public float EndTime => StartTime + RepeatDuration * repeats;
    public TValue StartValue => command.StartValue;
    public TValue EndValue => command.EndValue;

    public bool Active => true;
    public int Cost => throw new InvalidOperationException();

    public TValue ValueAtTime(float time)
    {
        if (time < StartTime) return command.ValueAtTime(command.StartTime);
        if (EndTime < time) return command.ValueAtTime(command.EndTime);

        var repeatDuration = RepeatDuration;
        var repeatTime = time - StartTime;
        var repeated = repeatTime > repeatDuration;
        repeatTime %= repeatDuration;

        if (repeated && repeatTime < command.StartTime)
            return command.ValueAtTime(repeated ? command.EndTime : command.StartTime);

        if (command.EndTime < repeatTime) return command.ValueAtTime(command.EndTime);
        return command.ValueAtTime(repeatTime);
    }

    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);

    public void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation)
        => throw new InvalidOperationException();

    public override string ToString() => $"loop x{repeats} ({StartTime}s - {EndTime}s)";
}