namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Linq;

#pragma warning disable CS1591
public class TriggerCommand : CommandGroup
{
    public TriggerCommand(string triggerName, float startTime, float endTime, int group = 0)
    {
        TriggerName = triggerName;
        StartTime = startTime;
        EndTime = endTime;
        Group = group;
    }

    public string TriggerName { get; set; }
    public int Group { get; set; }
    public override bool Active => false;

    protected override string GetCommandGroupHeader(ExportSettings exportSettings)
        => $"T,{TriggerName},{((int)StartTime).ToString(exportSettings.NumberFormat)},{((int)EndTime).ToString(exportSettings.NumberFormat)},{Group.ToString(exportSettings.NumberFormat)}";

    public override int GetHashCode()
    {
        HashCode header = new();
        header.Add('T');
        header.Add(TriggerName);
        header.Add(StartTime);
        header.Add(EndTime);
        header.Add(Group);
        foreach (var command in commands) header.Add(command);
        return header.ToHashCode();
    }

    public override bool Equals(object obj) => obj is TriggerCommand loop && Equals(loop);

    public bool Equals(TriggerCommand other) => other.TriggerName == TriggerName &&
        other.StartTime == StartTime &&
        other.EndTime == EndTime &&
        other.Group == Group &&
        commands.SequenceEqual(other.commands);
}