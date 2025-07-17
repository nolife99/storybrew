namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

#pragma warning disable CS1591
public abstract class CommandGroup : ICommand
{
    private protected readonly List<ICommand> commands = [];
    public ReadOnlySpan<ICommand> Commands => CollectionsMarshal.AsSpan(commands);

    public float CommandsStartTime
    {
        get
        {
            var commandsStartTime = float.MaxValue;
            foreach (var command in commands) commandsStartTime = float.Min(commandsStartTime, command.StartTime);

            return commandsStartTime;
        }
    }

    public float CommandsEndTime
    {
        get
        {
            var commandsEndTime = float.MinValue;
            foreach (var command in commands) commandsEndTime = float.Max(commandsEndTime, command.EndTime);

            return commandsEndTime;
        }
    }

    public float CommandsDuration
    {
        get
        {
            var commandsStartTime = float.MaxValue;
            var commandsEndTime = float.MinValue;

            foreach (var command in commands)
            {
                commandsStartTime = float.Min(commandsStartTime, command.StartTime);

                commandsEndTime = float.Max(commandsEndTime, command.EndTime);
            }

            return commandsEndTime - commandsStartTime;
        }
    }

    public float StartTime { get; protected set; }
    public virtual float EndTime { get; protected set; }

    public int CompareTo(ICommand other)
    {
        var result = StartTime.CompareTo(other.StartTime);
        return result != 0 ? result : EndTime.CompareTo(other.EndTime);
    }

    void ICommand.WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation)
    {
        if (commands.Count <= 0) return;

        commands.TrimExcess();

        for (var i = 0; i < indentation; ++i) writer.Write(' ');

        using (var header = GetCommandGroupHeader(ExportSettings.Default)) writer.WriteLine(header.AsReadOnlySpan());

        foreach (var command in commands) command.WriteOsb(writer, exportSettings, transform, indentation + 1);
    }

    public abstract bool IsFragmentableAt(float time);

    public bool Add(ICommand command)
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
            if (commands[index].StartTime < command.StartTime) break;

            ++index;
        }

        commands.Insert(index, command);
        return true;
    }

    public virtual void EndGroup() { }
    protected abstract TempList<char> GetCommandGroupHeader(ExportSettings exportSettings);

    public override string ToString()
    {
        using var header = GetCommandGroupHeader(ExportSettings.Default);
        return $"{header.AsReadOnlySpan()} ({commands.Count} commands)";
    }
}