namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.IO;
using Commands;
using CommandValues;

#pragma warning disable CS1591
public class TriggerDecorator<TValue>(ITypedCommand<TValue> command) : ITypedCommand<TValue> where TValue : CommandValue
{
    float triggerTime;

    public TValue StartValue => command.StartValue;
    public TValue EndValue => command.EndValue;
    public float StartTime => command.StartTime;
    public float EndTime => command.EndTime;

    public bool Active { get; set; }
    public int Cost => throw new NotImplementedException();

    public TValue ValueAtTime(float time)
    {
        if (!Active) throw new InvalidOperationException("Not triggered");

        return time < command.StartTime ?
            command.ValueAtTime(command.StartTime) :
            command.ValueAtTime(command.EndTime < time ? command.EndTime : time);
    }
    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);

    public void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation) { }
    public event EventHandler OnStateChanged;

    public override string ToString() => $"triggerable ({StartTime}s - {EndTime}s active:{Active})";
}