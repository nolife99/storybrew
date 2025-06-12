namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Linq;
using CommandValues;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

#pragma warning disable CS1591
public sealed class TriggerCommand : CommandGroup
{
    public TriggerCommand(string triggerName, float startTime, float endTime, int group = 0)
    {
        TriggerName = triggerName;
        StartTime = startTime;
        EndTime = endTime;
        Group = group;
    }

    public string TriggerName { get; }
    public int Group { get; }

    protected override TempList<char> GetCommandGroupHeader(ExportSettings exportSettings)
    {
        var list = TempList<char>.Create();
        list.AddRange(['T', ',']);
        list.AddRange(TriggerName.AsSpan());

        using (var startTimeString =
            (exportSettings.UseFloatForTime ? (CommandDecimal)StartTime : (CommandDecimal)float.Round(StartTime))
            .ToOsbString(exportSettings)) list.AddRange(startTimeString.AsReadOnlySpan());

        list.Add(',');
        using (var endTimeString =
            (exportSettings.UseFloatForTime ? (CommandDecimal)StartTime : (CommandDecimal)float.Round(EndTime)).ToOsbString(
                exportSettings)) list.AddRange(endTimeString.AsReadOnlySpan());

        list.Add(',');
        using (var groupString = ((CommandDecimal)Group).ToOsbString(exportSettings))
            list.AddRange(groupString.AsReadOnlySpan());

        return list;
    }

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