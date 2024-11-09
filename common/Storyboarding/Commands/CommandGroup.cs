namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Collections.Generic;
using System.IO;

#pragma warning disable CS1591
public abstract class CommandGroup : ICommand
{
    protected readonly HashSet<ICommand> commands = [];
    bool ended;
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

    public float StartTime { get; set; }
    public virtual float EndTime { get; set; }
    public virtual bool Active => true;
    public int Cost => commands.Count;
    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);

    public void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation)
    {
        if (commands.Count <= 0) return;

        writer.WriteLine(new string(' ', indentation) + GetCommandGroupHeader(exportSettings));
        foreach (var command in commands) command.WriteOsb(writer, exportSettings, transform, indentation + 1);
    }

    public bool Contains(ICommand command) => commands.Contains(command);
    public bool Add(ICommand command) => commands.Add(command);

    public virtual void EndGroup() => ended = true;
    protected abstract string GetCommandGroupHeader(ExportSettings exportSettings);

    public override string ToString() => $"{GetCommandGroupHeader(ExportSettings.Default)} ({commands.Count} commands)";
}