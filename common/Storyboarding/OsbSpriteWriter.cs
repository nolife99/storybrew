using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding.Display;

namespace StorybrewCommon.Storyboarding;

///<summary> A helper class for writing and exporting a storyboard. </summary>
public class OsbSpriteWriter(OsbSprite sprite,
    AnimatedValue<CommandPosition> move, AnimatedValue<CommandDecimal> moveX, AnimatedValue<CommandDecimal> moveY,
    AnimatedValue<CommandDecimal> scale, AnimatedValue<CommandScale> scaleVec,
    AnimatedValue<CommandDecimal> rotate,
    AnimatedValue<CommandDecimal> fade,
    AnimatedValue<CommandColor> color,
    TextWriter writer, ExportSettings exportSettings, OsbLayer layer)
{
#pragma warning disable CS1591
    protected readonly TextWriter TextWriter = writer;
    protected readonly ExportSettings ExportSettings = exportSettings;
    protected readonly OsbLayer Layer = layer;

    public void WriteOsb(StoryboardTransform transform)
    {
        if (ExportSettings.OptimiseSprites && sprite.CommandSplitThreshold > 0 && sprite.CommandCount > sprite.CommandSplitThreshold && IsFragmentable())
        {
            HashSet<IFragmentableCommand> commands = [];
            foreach (var cmd in sprite.Commands) commands.Add((IFragmentableCommand)cmd);
            var fragmentationTimes = GetFragmentationTimes(commands);

            while (commands.Count > 0)
            {
                var segment = getNextSegment(fragmentationTimes, commands);
                var sprite = CreateSprite(segment);
                writeOsbSprite(sprite, transform);
            }
        }
        else writeOsbSprite(sprite, transform);
    }
    protected virtual OsbSprite CreateSprite(ICollection<IFragmentableCommand> segment)
    {
        OsbSprite spr = new()
        {
            TexturePath = sprite.TexturePath,
            InitialPosition = sprite.InitialPosition,
            Origin = sprite.Origin
        };
        foreach (var command in segment) spr.AddCommand(command);
        return spr;
    }
    void writeOsbSprite(OsbSprite sprite, StoryboardTransform transform)
    {
        WriteHeader(sprite, transform);
        foreach (var command in sprite.Commands) command.WriteOsb(TextWriter, ExportSettings, transform, 1);
    }
    protected virtual void WriteHeader(OsbSprite sprite, StoryboardTransform transform)
    {
        TextWriter.Write($"Sprite");
        WriteHeaderCommon(sprite, transform);
        TextWriter.WriteLine();
    }
    protected virtual void WriteHeaderCommon(OsbSprite sprite, StoryboardTransform transform)
    {
        TextWriter.Write($",{layer},{sprite.Origin},\"{sprite.TexturePath.Trim()}\"");

        var transformedInitialPosition = transform is null ?
            (Vector2)sprite.InitialPosition : sprite.HasMoveXYCommands ? transform.ApplyToPositionXY(sprite.InitialPosition) : transform.ApplyToPosition(sprite.InitialPosition);

        if (!move.HasCommands && !moveX.HasCommands) TextWriter.Write($",{transformedInitialPosition.X.ToString(ExportSettings.NumberFormat)}");
        else TextWriter.Write($",0");
        if (!move.HasCommands && !moveY.HasCommands) TextWriter.Write($",{transformedInitialPosition.Y.ToString(ExportSettings.NumberFormat)}");
        else TextWriter.Write($",0");
    }
    protected virtual bool IsFragmentable()
    {
        // if there are commands with nondeterministic results (aka triggercommands) the sprite can't reliably be split
        if (sprite.Commands.Any(c => c is not IFragmentableCommand)) return false;

        return !(move.HasOverlap || moveX.HasOverlap || moveY.HasOverlap ||
            rotate.HasOverlap ||
            scale.HasOverlap || scaleVec.HasOverlap ||
            fade.HasOverlap ||
            color.HasOverlap);
    }
    protected virtual HashSet<int> GetFragmentationTimes(IEnumerable<IFragmentableCommand> fragCommands)
    {
        HashSet<int> fragTimes = new(Enumerable.Range((int)sprite.StartTime, (int)(sprite.EndTime - sprite.StartTime) + 1));
        foreach (var command in fragCommands) fragTimes.ExceptWith(command.GetNonFragmentableTimes());
        return fragTimes;
    }
    HashSet<IFragmentableCommand> getNextSegment(HashSet<int> fragmentationTimes, HashSet<IFragmentableCommand> commands)
    {
        HashSet<IFragmentableCommand> segment = [];

        var startTime = fragmentationTimes.Min();
        var endTime = getSegmentEndTime(fragmentationTimes, commands);

        foreach (var cmd in commands) if (cmd.StartTime < endTime)
            {
                var sTime = Math.Max(startTime, (int)Math.Round(cmd.StartTime));
                var eTime = Math.Min(endTime, (int)Math.Round(cmd.EndTime));

                segment.Add(sTime != (int)Math.Round(cmd.StartTime) || eTime != (int)Math.Round(cmd.EndTime) ? cmd.GetFragment(sTime, eTime) : cmd);
            }
        addStaticCommands(segment, startTime);

        fragmentationTimes.RemoveWhere(t => t < endTime);
        commands.RemoveWhere(c => c.EndTime <= endTime);
        return segment;
    }
    int getSegmentEndTime(HashSet<int> fragmentationTimes, ICollection<IFragmentableCommand> commands)
    {
        var startTime = fragmentationTimes.Min();
        int endTime;
        var maxCommandCount = sprite.CommandSplitThreshold;

        if (commands.Count < sprite.CommandSplitThreshold * 2 && commands.Count > sprite.CommandSplitThreshold) maxCommandCount = (int)Math.Ceiling(commands.Count / 2f);
        if (commands.Count < maxCommandCount) endTime = fragmentationTimes.Max() + 1;
        else
        {
            var lastCommand = commands.OrderBy(c => c.StartTime).ElementAt(maxCommandCount - 1);
            if (fragmentationTimes.Contains((int)lastCommand.StartTime) && lastCommand.StartTime > startTime) endTime = (int)lastCommand.StartTime;
            else
            {
                if (fragmentationTimes.Any(t => t < (int)lastCommand.StartTime))
                {
                    endTime = fragmentationTimes.Where(t => t < (int)lastCommand.StartTime).Max();
                    if (endTime == startTime) endTime = fragmentationTimes.First(t => t > startTime);
                }
                else endTime = fragmentationTimes.First(t => t > startTime);
            }
        }
        return endTime;
    }
    void addStaticCommands(ICollection<IFragmentableCommand> segment, int startTime)
    {
        if (move.HasCommands && !segment.Any(c => c is MoveCommand && c.StartTime == startTime))
        {
            var value = move.ValueAtTime(startTime);
            segment.Add(new MoveCommand(OsbEasing.None, startTime, startTime, value, value));
        }
        if (moveX.HasCommands && !segment.Any(c => c is MoveXCommand && c.StartTime == startTime))
        {
            var value = moveX.ValueAtTime(startTime);
            segment.Add(new MoveXCommand(OsbEasing.None, startTime, startTime, value, value));
        }
        if (moveY.HasCommands && !segment.Any(c => c is MoveYCommand && c.StartTime == startTime))
        {
            var value = moveY.ValueAtTime(startTime);
            segment.Add(new MoveYCommand(OsbEasing.None, startTime, startTime, value, value));
        }
        if (rotate.HasCommands && !segment.Any(c => c is RotateCommand && c.StartTime == startTime))
        {
            var value = rotate.ValueAtTime(startTime);
            segment.Add(new RotateCommand(OsbEasing.None, startTime, startTime, value, value));
        }
        if (scale.HasCommands && !segment.Any(c => c is ScaleCommand && c.StartTime == startTime))
        {
            var value = scale.ValueAtTime(startTime);
            segment.Add(new ScaleCommand(OsbEasing.None, startTime, startTime, value, value));
        }
        if (scaleVec.HasCommands && !segment.Any(c => c is VScaleCommand && c.StartTime == startTime))
        {
            var value = scaleVec.ValueAtTime(startTime);
            segment.Add(new VScaleCommand(OsbEasing.None, startTime, startTime, value, value));
        }
        if (color.HasCommands && !segment.Any(c => c is ColorCommand && c.StartTime == startTime))
        {
            var value = color.ValueAtTime(startTime);
            segment.Add(new ColorCommand(OsbEasing.None, startTime, startTime, value, value));
        }
        if (fade.HasCommands && !segment.Any(c => c is FadeCommand && c.StartTime == startTime))
        {
            var value = fade.ValueAtTime(startTime);
            segment.Add(new FadeCommand(OsbEasing.None, startTime, startTime, value, value));
        }
    }
}
public static class OsbWriterFactory
{
    public static OsbSpriteWriter CreateWriter(OsbSprite sprite,
        AnimatedValue<CommandPosition> move, AnimatedValue<CommandDecimal> moveX, AnimatedValue<CommandDecimal> moveY,
        AnimatedValue<CommandDecimal> scale, AnimatedValue<CommandScale> scaleVec,
        AnimatedValue<CommandDecimal> rotate,
        AnimatedValue<CommandDecimal> fade, AnimatedValue<CommandColor> color,
        TextWriter writer, ExportSettings exportSettings, OsbLayer layer)
    {
        if (sprite is OsbAnimation animation) return new OsbAnimationWriter(animation,
            move, moveX, moveY,
            scale, scaleVec,
            rotate,
            fade,
            color,
            writer, exportSettings, layer);

        else return new OsbSpriteWriter(sprite,
            move, moveX, moveY,
            scale, scaleVec,
            rotate,
            fade, color,
            writer, exportSettings, layer);
    }
}