namespace StorybrewCommon.Storyboarding.Display;

using System.Collections.Generic;
using Collections.Pooled;
using Commands;
using CommandValues;

internal class CommandChannel<TValue> where TValue : struct, CommandValue
{
    readonly PooledList<ITypedCommand<TValue>> commands = [];
    public IReadOnlyList<ITypedCommand<TValue>> Commands => commands;
    public bool HasOverlap { get; private set; }

    public ITypedCommand<TValue> StartCommand => commands.Count != 0 ? commands[0] : null;
    public ITypedCommand<TValue> EndCommand => commands.Count != 0 ? commands[^1] : null;

    public virtual CommandResult<TValue> StartResult => StartCommand.AsResult();
    public virtual CommandResult<TValue> EndResult => EndCommand.AsResult();

    public void Add(ITypedCommand<TValue> command)
    {
        findCommandIndex(command.StartTime, out var index);
        while (index < commands.Count)
        {
            if (commands[index].CompareTo(command) > 0) break;

            index++;
        }

        HasOverlap |= index > 0 && (int)float.Round(command.StartTime) < (int)float.Round(commands[index - 1].EndTime) ||
            index < commands.Count && (int)float.Round(commands[index].StartTime) < (int)float.Round(command.EndTime);

        commands.Insert(index, command);
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
}