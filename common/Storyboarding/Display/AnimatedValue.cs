using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace StorybrewCommon.Storyboarding.Display;

#pragma warning disable CS1591
public class AnimatedValue<TValue> where TValue : CommandValue
{
    public TValue DefaultValue;
    readonly List<ITypedCommand<TValue>> commands = [];

    public bool HasCommands => commands.Count > 0;
    public bool HasOverlap { get; private set; }

    public double StartTime => commands.Count > 0 ? commands[0].StartTime : 0;
    public double EndTime => commands.Count > 0 ? commands[^1].EndTime : 0;
    public double Duration => EndTime - StartTime;
    public TValue StartValue => commands.Count > 0 ? commands[0].StartValue : DefaultValue;
    public TValue EndValue => commands.Count > 0 ? commands[^1].EndValue : DefaultValue;

    public AnimatedValue() { }
    public AnimatedValue(TValue defaultValue) => DefaultValue = defaultValue;

    public void Add(ITypedCommand<TValue> command)
    {
        if (command is not TriggerDecorator<TValue> triggerable)
        {
            var found = findCommandIndex(command.StartTime, out int index);
            var span = CollectionsMarshal.AsSpan(commands);
            while (index < span.Length)
            {
                if (span[index].CompareTo(command) < 0) ++index;
                else break;
            }

            HasOverlap |=
                (index > 0 && Math.Round(command.StartTime) < Math.Round(span[index - 1].EndTime)) ||
                (index < span.Length && Math.Round(span[index].StartTime) < Math.Round(command.EndTime));

            commands.Insert(index, command);
        }
        else triggerable.OnStateChanged += triggerable_OnStateChanged;
    }
    public void Remove(ITypedCommand<TValue> command)
    {
        if (command is not TriggerDecorator<TValue> triggerable) commands.Remove(command);
        else triggerable.OnStateChanged -= triggerable_OnStateChanged;
    }

    public bool IsActive(double time) => commands.Count > 0 && StartTime <= time && time <= EndTime;
    public TValue ValueAtTime(double time)
    {
        if (commands.Count == 0) return DefaultValue;
        var span = CollectionsMarshal.AsSpan(commands);

        if (!findCommandIndex(time, out int index) && index > 0) --index;
        if (HasOverlap) for (var i = 0; i < index; ++i) if (time < span[i].EndTime)
        {
            index = i;
            break;
        }

        return commands[index].ValueAtTime(time);
    }
    bool findCommandIndex(double time, out int index)
    {
        var left = 0;
        var span = CollectionsMarshal.AsSpan(commands);
        var right = span.Length - 1;

        while (left <= right)
        {
            index = left + ((right - left) >> 1);
            var commandTime = span[index].StartTime;
            if (commandTime == time) return true;
            else if (commandTime < time) left = index + 1;
            else right = index - 1;
        }
        index = left;
        return false;
    }
    void triggerable_OnStateChanged(object sender, EventArgs e)
    {
        var command = (ITypedCommand<TValue>)sender;

        commands.Remove(command);
        if (command.Active)
        {
            findCommandIndex(command.StartTime, out int index);
            commands.Insert(index, command);
        }
    }
}