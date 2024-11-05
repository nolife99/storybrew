using System;
using System.IO;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Storyboarding.Display;

#pragma warning disable CS1591
public class TriggerDecorator<TValue>(ITypedCommand<TValue> command) : ITypedCommand<TValue> where TValue : CommandValue
{
    float triggerTime;

    public OsbEasing Easing => throw new NotImplementedException();
    public float StartTime => triggerTime + command.StartTime;
    public float EndTime => triggerTime + command.EndTime;
    public TValue StartValue => command.StartValue;
    public TValue EndValue => command.EndValue;
    public float Duration => EndTime - StartTime;
    public bool Active { get; set; }
    public int Cost => throw new NotImplementedException();

    public event EventHandler OnStateChanged;

    public void Trigger(float time)
    {
        if (Active) return;

        Active = true;
        triggerTime = time;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }
    public void UnTrigger()
    {
        if (!Active) return;

        Active = false;
        OnStateChanged?.Invoke(this, EventArgs.Empty);
    }
    public TValue ValueAtTime(float time)
    {
        if (!Active) throw new InvalidOperationException("Not triggered");

        var commandTime = time - triggerTime;
        if (commandTime < command.StartTime) return command.ValueAtTime(command.StartTime);
        if (command.EndTime < commandTime) return command.ValueAtTime(command.EndTime);
        return command.ValueAtTime(commandTime);
    }
    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);

    public void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation) => throw new NotImplementedException();
    public override string ToString() => $"triggerable ({StartTime}s - {EndTime}s active:{Active})";
}