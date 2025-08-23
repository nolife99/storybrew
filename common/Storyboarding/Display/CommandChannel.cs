namespace StorybrewCommon.Storyboarding.Display;

using System;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

class CommandChannel<TValue> where TValue : struct, ICommandValue<TValue>
{
    protected ValueList<Command<TValue>> commands = ValueList.Create<Command<TValue>>();

    public bool HasOverlap;

    public ReadOnlySpan<Command<TValue>> Commands => commands.AsReadOnlySpan();

    public bool Add(Command<TValue> command)
    {
        var c = commands.AsSpan();

        var index = c.BinarySearch(command);
        if (index >= 0)
        {
            c[index] = command;
            return false;
        }

        index = ~index;
        while (index < c.Length)
        {
            if (c[index].CompareTo(command) > 0) break;

            ++index;
        }

        HasOverlap |= index > 0 && command.startTime < c[index - 1].endTime ||
            index < c.Length && c[index].startTime < command.endTime;

        commands.Insert(index, command);

        return true;
    }

    protected Command<TValue> CommandAtTime(float time)
    {
        var c = commands.AsReadOnlySpan();
        if (c.Length == 0) return null;

        if (!findCommandIndex(c, time, out var index) && index > 0) --index;

        if (HasOverlap)
        {
            for (var i = 0; i < index; i++)
                if (c[i].StartTime <= c[index].startTime && time <= c[i].endTime)
                {
                    index = i;
                    break;
                }
        }
        else if (index > 0 && time == c[index - 1].endTime) --index;

        return c[index];
    }

    public virtual bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        var command = CommandAtTime(time);
        if (command is null)
        {
            result = default;
            return false;
        }

        result = new(command);
        return true;
    }

    static bool findCommandIndex(ReadOnlySpan<Command<TValue>> c, float time, out int index)
    {
        var left = 0;
        var right = c.Length - 1;

        while (left <= right)
        {
            var currentIndex = right + left >> 1;
            var commandTime = c[currentIndex].startTime;

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