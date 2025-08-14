namespace StorybrewCommon.Storyboarding.Display;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BrewLib.Util;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;

class CommandChannel<TValue> where TValue : struct, ICommandValue
{
    protected readonly List<CommandResult<TValue>> commands = [];

    public IReadOnlyList<CommandResult<TValue>> Commands => commands;

    public bool HasOverlap { get; private set; }

    public bool Add(Command<TValue> command)
    {
        var result = command.AsResult();

        var index = commands.BinarySearch(result);
        if (index >= 0)
        {
            commands[index] = result;
            return false;
        }

        index = ~index;
        while (index < commands.Count)
        {
            if (commands[index].Command.CompareTo(command) > 0) break;

            ++index;
        }

        HasOverlap |= index > 0 && result.StartTime < commands[index - 1].EndTime ||
            index < commands.Count && commands[index].StartTime < result.EndTime;

        commands.Insert(index, result);

        return true;
    }

    protected Command<TValue> CommandAtTime(float time)
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
        else if (index > 0 && time == commands[index - 1].EndTime) --index;

        return commands[index].Command;
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
        var c = commands.GetSpanUnsafe();

        var left = 0;
        var right = c.Length - 1;

        ref var first = ref MemoryMarshal.GetReference(c);
        while (left <= right)
        {
            var currentIndex = right + left >> 1;
            var commandTime = Unsafe.Add(ref first, currentIndex).StartTime;

            if (commandTime > time) right = currentIndex - 1;
            else if (commandTime < time) left = currentIndex + 1;
            else
            {
                index = currentIndex;
                return true;
            }
        }

        index = left;
        return false;
    }
}