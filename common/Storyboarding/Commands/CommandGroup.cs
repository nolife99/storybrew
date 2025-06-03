namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

#pragma warning disable CS1591
public abstract class CommandGroup : ICommand
{
    protected internal readonly PooledHashSet<ICommand> commands = new();
    public IReadOnlyCollection<ICommand> Commands => commands;

    public float CommandsStartTime
    {
        get
        {
            var commandsStartTime = float.MaxValue;
            foreach (var command in commands) commandsStartTime = Math.Min(commandsStartTime, command.StartTime);

            return commandsStartTime;
        }
    }

    public float CommandsEndTime
    {
        get
        {
            var commandsEndTime = float.MinValue;
            foreach (var command in commands) commandsEndTime = Math.Max(commandsEndTime, command.EndTime);

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
                commandsStartTime = Math.Min(commandsStartTime, command.StartTime);

                commandsEndTime = Math.Max(commandsEndTime, command.EndTime);
            }

            return commandsEndTime - commandsStartTime;
        }
    }

    public float StartTime { get; protected set; }
    public virtual float EndTime { get; protected set; }
    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);

    void ICommand.WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation)
    {
        if (commands.Count <= 0) return;

        Span<char> indent = stackalloc char[indentation];
        indent.Fill(' ');

        writer.Write(indent);

        using (var header = GetCommandGroupHeader(ExportSettings.Default)) writer.WriteLine(header.AsReadOnlySpan());

        foreach (var command in commands) command.WriteOsb(writer, exportSettings, transform, indentation + 1);
    }

    public bool Add(ICommand command) => commands.Add(command);
    public virtual void EndGroup() { }
    protected abstract TempList<char> GetCommandGroupHeader(ExportSettings exportSettings);

    public override string ToString()
    {
        using var header = GetCommandGroupHeader(ExportSettings.Default);
        return $"{header.AsReadOnlySpan()} ({commands.Count} commands)";
    }
}