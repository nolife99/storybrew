using System;
using System.Collections.Generic;
using System.IO;

namespace StorybrewCommon.Storyboarding.Commands;

#pragma warning disable CS1591
public abstract class CommandGroup : ICommand
{
    bool ended;

    public float StartTime { get; set; }
    public virtual float EndTime { get; set; }
    public virtual bool Active => true;
    public int Cost => commands.Count;

    protected readonly HashSet<ICommand> commands = [];
    public IEnumerable<ICommand> Commands => commands;

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

    public bool Contains(ICommand command) => commands.Contains(command);
    public bool Add(ICommand command) => ended ? throw new InvalidOperationException("Cannot add commands to a group after it ended") : commands.Add(command);

    public virtual void EndGroup() => ended = true;
    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);

    public void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation)
    {
        if (commands.Count <= 0) return;

        writer.WriteLine(new string(' ', indentation) + GetCommandGroupHeader(exportSettings));
        foreach (var command in commands) command.WriteOsb(writer, exportSettings, transform, indentation + 1);
    }
    protected abstract string GetCommandGroupHeader(ExportSettings exportSettings);

    public override string ToString() => $"{GetCommandGroupHeader(ExportSettings.Default)} ({commands.Count} commands)";
}