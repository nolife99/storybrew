namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Commands;
using CommandValues;

#pragma warning disable CS1591
public class AnimatedValue<TValue> where TValue : CommandValue
{
    readonly List<ITypedCommand<TValue>> commands = [];
    public AnimatedValue() { }
    public AnimatedValue(TValue defaultValue) => DefaultValue = defaultValue;
    public TValue DefaultValue { get; internal set; }
    public bool HasCommands => commands.Count > 0;
    public bool HasOverlap { get; private set; }
    public float StartTime => commands.Count > 0 ? commands[0].StartTime : 0;
    public float EndTime => commands.Count > 0 ? commands[^1].EndTime : 0;
    public float Duration => EndTime - StartTime;
    public TValue StartValue => commands.Count > 0 ? commands[0].StartValue : DefaultValue;
    public TValue EndValue => commands.Count > 0 ? commands[^1].EndValue : DefaultValue;
    public void Add(ITypedCommand<TValue> command)
    {
        if (command is not TriggerDecorator<TValue> triggerable)
        {
            var span = CollectionsMarshal.AsSpan(commands);
            findCommandIndex(span, command.StartTime, out var index);

            while (index < span.Length)
                if (span[index].CompareTo(command) < 0) ++index;
                else break;

            HasOverlap |= index > 0 && Math.Round(command.StartTime) < Math.Round(span[index - 1].EndTime) ||
                index < span.Length && Math.Round(span[index].StartTime) < Math.Round(command.EndTime);

            commands.Insert(index, command);
        }
        else triggerable.OnStateChanged += triggerable_OnStateChanged;
    }
    public TValue ValueAtTime(float time)
    {
        if (commands.Count == 0) return DefaultValue;

        var span = CollectionsMarshal.AsSpan(commands);

        if (!findCommandIndex(span, time, out var index) && index > 0) --index;
        if (!HasOverlap) return span[index].ValueAtTime(time);

        for (var i = 0; i < index; ++i)
            if (time < span[i].EndTime)
            {
                index = i;
                break;
            }

        return span[index].ValueAtTime(time);
    }
    static bool findCommandIndex(ReadOnlySpan<ITypedCommand<TValue>> commands, float time, out int index)
    {
        var left = 0;
        var right = commands.Length - 1;

        while (left <= right)
        {
            index = left + (right - left >> 1);
            var commandTime = commands[index].StartTime;
            if (commandTime == time) return true;
            if (commandTime < time) left = index + 1;
            else right = index - 1;
        }

        index = left;

        return false;
    }
    void triggerable_OnStateChanged(object sender, EventArgs e)
    {
        var command = (ITypedCommand<TValue>)sender;
        commands.Remove(command);
        if (!command.Active) return;

        findCommandIndex(CollectionsMarshal.AsSpan(commands), command.StartTime, out var index);
        commands.Insert(index, command);
    }
}