namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Linq;
using BrewLib.Util;
using CommandValues;
using Tiny.PooledCollections.Generic.Temporary;

#pragma warning disable CS1591
public sealed class LoopCommand : CommandGroup
{
    public LoopCommand(float startTime, int loopCount)
    {
        StartTime = startTime;
        LoopCount = loopCount;
    }

    public int LoopCount { get; private set; }

    public override float EndTime
    {
        get => StartTime + CommandsEndTime * LoopCount;
        protected set => LoopCount = (int)((value - StartTime) / CommandsEndTime);
    }

    public override int GetHashCode()
    {
        HashCode header = new();
        header.Add('L');
        header.Add(StartTime);
        header.Add(LoopCount);
        foreach (var command in commands) header.Add(command);
        return header.ToHashCode();
    }

    public override void EndGroup()
    {
        var commandsStartTime = CommandsStartTime;
        if (commandsStartTime > 0)
        {
            StartTime += commandsStartTime;
            foreach (var command in commands) ((IOffsetable)command).Offset(-commandsStartTime);
        }

        base.EndGroup();
    }

    public override bool IsFragmentableAt(float time)
    {
        for (var i = 1; i < LoopCount - 1; i++)
            if (time == StartTime + i * CommandsEndTime)
                return true;

        return false;
    }

    protected override TempList<char> GetCommandGroupHeader(ExportSettings exportSettings) => StringHelper.Interpolate(
        exportSettings.NumberFormat,
        $"L,{(CommandDecimal)(exportSettings.UseFloatForTime ? StartTime : float.Round(StartTime))},{LoopCount}");

    public override bool Equals(object obj) => obj is LoopCommand loop && Equals(loop);

    public bool Equals(LoopCommand other) => other.StartTime == StartTime &&
        other.LoopCount == LoopCount &&
        commands.SequenceEqual(other.commands);
}