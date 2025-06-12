namespace StorybrewCommon.Storyboarding.Display;

using System;
using Commands;
using CommandValues;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;

internal class CommandChannel<TValue> where TValue : struct, ICommandValue
{
    readonly PooledList<ITypedCommand<TValue>> commands = [];
    public ReadOnlySpan<ITypedCommand<TValue>> Commands => commands.AsReadOnlySpan();
    public bool HasOverlap { get; private set; }

    public ITypedCommand<TValue> StartCommand => commands.Count != 0 ? commands[0] : null;
    public ITypedCommand<TValue> EndCommand => commands.Count != 0 ? commands[^1] : null;

    public virtual CommandResult<TValue> StartResult => StartCommand.AsResult();
    public virtual CommandResult<TValue> EndResult => EndCommand.AsResult();

    internal bool Add(ITypedCommand<TValue> command)
    {
        var index = commands.BinarySearch(command);
        if (index >= 0)
        {
            commands[index] = command;
            return false;
        }

        index = ~index;
        while (index < commands.Count)
        {
            if (commands[index].CompareTo(command) > 0) break;

            ++index;
        }

        HasOverlap |= index > 0 && (int)float.Round(command.StartTime) < (int)float.Round(commands[index - 1].EndTime) ||
            index < commands.Count && (int)float.Round(commands[index].StartTime) < (int)float.Round(command.EndTime);

        commands.Insert(index, command);
        return true;
    }

    public ITypedCommand<TValue> CommandAtTime(float time)
    {
        if (commands.Count == 0) return null;

        if (!findCommandIndex(time, out var index) && index > 0) --index;

        if (HasOverlap)
        {
            for (var i = 0; i < index; i++)
                if (commands[i].StartTime <= commands[index].StartTime && time <= commands[i].EndTime)
                {
                    index = i;
                    break;
                }
        }
        else if (index > 0 && (int)float.Round(time) == (int)float.Round(commands[index - 1].EndTime)) --index;

        return commands[index];
    }

    public virtual bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        var command = CommandAtTime(time);
        if (command is null)
        {
            result = default;
            return false;
        }

        result = command.AsResult();
        return true;
    }

    bool findCommandIndex(float time, out int index)
    {
        var span = commands.AsReadOnlySpan();

        var left = 0;
        var right = span.Length - 1;
        while (left <= right)
        {
            index = left + (right - left >> 1);
            var commandTime = span[index].StartTime;
            if (commandTime == time) return true;

            if (commandTime < time) left = index + 1;
            else right = index - 1;
        }

        index = left;
        return false;
    }
}