namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.Collections.Generic;
using Commands;
using CommandValues;

#pragma warning disable CS1591
public class AnimatedValue<TValue> where TValue : CommandValue
{
    readonly List<ITypedCommand<TValue>> commands = [];
    public TValue DefaultValue;

    public AnimatedValue() { }
    public AnimatedValue(TValue defaultValue) => DefaultValue = defaultValue;

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
            findCommandIndex(command.StartTime, out var index);
            while (index < commands.Count)
                if (commands[index].CompareTo(command) < 0) ++index;
                else break;

            HasOverlap |= index > 0 && Math.Round(command.StartTime) < Math.Round(commands[index - 1].EndTime) ||
                index < commands.Count && Math.Round(commands[index].StartTime) < Math.Round(command.EndTime);

            commands.Insert(index, command);
        }
        else triggerable.OnStateChanged += triggerable_OnStateChanged;
    }
    public TValue ValueAtTime(float time)
    {
        if (commands.Count == 0) return DefaultValue;

        if (!findCommandIndex(time, out var index) && index > 0) --index;
        if (!HasOverlap) return commands[index].ValueAtTime(time);

        for (var i = 0; i < index; ++i)
            if (time < commands[i].EndTime)
            {
                index = i;
                break;
            }

        return commands[index].ValueAtTime(time);
    }
    bool findCommandIndex(float time, out int index)
    {
        var left = 0;
        var right = commands.Count - 1;

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

        findCommandIndex(command.StartTime, out var index);
        commands.Insert(index, command);
    }
}