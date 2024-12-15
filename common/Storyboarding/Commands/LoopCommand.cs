namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS1591
public class LoopCommand : CommandGroup, IFragmentableCommand
{
    public LoopCommand(float startTime, int loopCount)
    {
        StartTime = startTime;
        LoopCount = loopCount;
    }
    public int LoopCount { get; set; }
    public bool IsFragmentable => LoopCount > 1;

    public override float EndTime
    {
        get => StartTime + CommandsEndTime * LoopCount;
        set => LoopCount = (int)((value - StartTime) / CommandsEndTime);
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
    public IFragmentableCommand GetFragment(float startTime, float endTime)
    {
        if (!IsFragmentable ||
            (endTime - startTime) % CommandsDuration != 0 ||
            (startTime - StartTime) % CommandsDuration != 0) return this;

        var loopCount = (int)float.Round((endTime - startTime) / CommandsDuration);
        LoopCommand loopFragment = new(startTime, loopCount);
        foreach (var c in commands) loopFragment.Add(c);
        return loopFragment;
    }
    public IEnumerable<int> GetNonFragmentableTimes()
    {
        var nonFragmentableTimes = new HashSet<int>(LoopCount * (int)(CommandsDuration - 1));
        for (var i = 0; i < LoopCount; i++)
        for (var j = 0; j < CommandsDuration - 1; ++j)
            nonFragmentableTimes.Add((int)StartTime + i * (int)CommandsDuration + 1 + j);

        return nonFragmentableTimes;
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
    protected override string GetCommandGroupHeader(ExportSettings exportSettings)
        => $"L,{(exportSettings.UseFloatForTime ? StartTime : (int)StartTime).ToString(exportSettings.NumberFormat)},{LoopCount.ToString(exportSettings.NumberFormat)}";
    public override bool Equals(object obj) => obj is LoopCommand loop && Equals(loop);
    public bool Equals(LoopCommand other)
        => other.StartTime == StartTime && other.LoopCount == LoopCount && commands.SequenceEqual(other.commands);
}