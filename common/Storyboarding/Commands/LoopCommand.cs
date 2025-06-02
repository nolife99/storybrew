namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.Linq;
using CommandValues;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals.Safe;

#pragma warning disable CS1591
public class LoopCommand : CommandGroup
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

    protected override TempList<char> GetCommandGroupHeader(ExportSettings exportSettings)
    {
        var list = TempList<char>.Create();
        list.AddRange(['L', ',']);

        using (var startTimeString =
            (exportSettings.UseFloatForTime ? (CommandDecimal)StartTime : (CommandDecimal)float.Round(StartTime))
            .ToOsbString(exportSettings)) list.AddRange(startTimeString.AsReadOnlySpan());

        list.Add(',');
        using (var groupString = ((CommandDecimal)LoopCount).ToOsbString(exportSettings))
            list.AddRange(groupString.AsReadOnlySpan());

        return list;
    }

    public override bool Equals(object obj) => obj is LoopCommand loop && Equals(loop);

    public bool Equals(LoopCommand other) => other.StartTime == StartTime &&
        other.LoopCount == LoopCount &&
        commands.SequenceEqual(other.commands);
}