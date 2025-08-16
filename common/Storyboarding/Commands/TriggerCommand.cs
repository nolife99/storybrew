namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Linq;
using BrewLib.Util;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Temporary;

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

    /// <inheritdoc/>
    public override bool IsFragmentableAt(float time) => false;

    protected override TempList<char> GetCommandGroupHeader(ExportSettings exportSettings)
        => StringHelper.Interpolate(exportSettings.NumberFormat,
            $"T,{TriggerName},{(CommandDecimal)(exportSettings.UseFloatForTime ? StartTime : float.Round(StartTime))},{(CommandDecimal)(exportSettings.UseFloatForTime ? StartTime : float.Round(EndTime))},{Group}");

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

    public bool Equals(TriggerCommand other)
        => other.TriggerName == TriggerName &&
            other.StartTime == StartTime &&
            other.EndTime == EndTime &&
            other.Group == Group &&
            commands.SequenceEqual(other.commands);
}