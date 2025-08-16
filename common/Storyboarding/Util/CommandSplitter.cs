namespace StorybrewCommon.Storyboarding.Util;

using System;
using System.Diagnostics;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using ZLinq;

public static class CommandSplitter
{
    public static void Split(OsbSprite sprite, StoryboardSegment segment, int commandSplitThreshold)
    {
        using var sprites = Split(sprite, segment.CreateSprite, segment.CreateAnimation, commandSplitThreshold);
        if (!sprites.Contains(sprite)) segment.Discard(sprite);
    }

    /* public static TempList<OsbSprite> Split(OsbSprite sprite, int commandSplitThreshold) => Split(sprite,
        (path, origin, initialPosition) => new()
        {
            TexturePath = path, Origin = origin, InitialPosition = initialPosition
        },
        (path, frameCount, frameDelay, loopType, origin, initialPosition) => new()
        {
            TexturePath = path,
            Origin = origin,
            FrameCount = frameCount,
            FrameDelay = frameDelay,
            LoopType = loopType,
            InitialPosition = initialPosition
        },
        commandSplitThreshold); */

    static TempList<OsbSprite> Split(OsbSprite sprite,
        Func<string, OsbOrigin, CommandPosition, OsbSprite> createSprite,
        Func<string, int, float, OsbLoopType, OsbOrigin, CommandPosition, OsbAnimation> createAnimation,
        int commandSplitThreshold)
    {
        var result = TempList.Create<OsbSprite>();

        if (sprite.HasIncompatibleCommands || sprite.HasTrigger || sprite.CommandCost < commandSplitThreshold)
        {
            result.Add(sprite);
            return result;
        }

        using var candidateFragmentTimes = getFragmentationTimes(sprite);
        candidateFragmentTimes.RemoveWhere(t => !canFragmentCommandsAt(sprite, t));

        using var distinctFragTimes = candidateFragmentTimes.AsReadOnlySpan()
            .AsValueEnumerable()
            .Select(s => s.Value)
            .Distinct()
            .Order()
            .ToArrayPool();

        var idealSegmentCount = int.Min((int)float.Ceiling((float)sprite.CommandCost / commandSplitThreshold),
            distinctFragTimes.Size + 1);

        if (idealSegmentCount < 2)
        {
            result.Add(sprite);
            return result;
        }

        using var segmentTimes = pickFragmentTimes(distinctFragTimes.Span,
            idealSegmentCount,
            sprite.StartTime,
            sprite.EndTime);

        segmentTimes.Insert(0, sprite.StartTime);
        segmentTimes.Add(sprite.EndTime);

        for (var i = 1; i < segmentTimes.Count; i++)
        {
            var segmentStart = segmentTimes[i - 1];
            var segmentEnd = segmentTimes[i];

            var segmentSprite = sprite switch
            {
                OsbAnimation animation when
                    animation.LoopType == OsbLoopType.LoopOnce && i == 1 ||
                    animation.LoopType == OsbLoopType.LoopForever => createAnimation(animation.TexturePath,
                        animation.FrameCount,
                        animation.FrameDelay,
                        animation.LoopType,
                        animation.Origin,
                        animation.InitialPosition),
                OsbAnimation { LoopType: OsbLoopType.LoopOnce } animation when i > 1 => createSprite(
                    animation.GetTexturePathAt(animation.StartTime + animation.FrameDelay * animation.FrameCount),
                    animation.Origin,
                    animation.InitialPosition),
                _ => createSprite(sprite.TexturePath, sprite.Origin, sprite.InitialPosition)
            };

            transferCommands(sprite, segmentSprite, segmentStart, segmentEnd);
            transferStartState(sprite, segmentSprite, segmentStart);

            result.Add(segmentSprite);
        }

        return result;
    }

    static TempList<float> pickFragmentTimes(ReadOnlySpan<float> candidates,
        int idealSegmentCount,
        float startTime,
        float endTime)
    {
        using var times = TempHashSet.Create<float>();
        var result = TempList.Create<float>();

        var idealSegmentDuration = (endTime - startTime) / idealSegmentCount;
        for (var i = 1; i < idealSegmentCount; i++)
        {
            var idealTime = startTime + i * idealSegmentDuration;
            var time = candidates.AsValueEnumerable().MinBy(candidate => float.Abs(candidate - idealTime));
            if (times.Add(time)) result.Add(time);
        }

        return result;
    }

    static void transferCommands(OsbSprite sprite, OsbSprite segmentSprite, float segmentStart, float segmentEnd)
    {
        foreach (var command in sprite.commandGroups.AsValueEnumerable()
            .Concat(sprite.displayValueBuilders.AsValueEnumerable()
                .SelectMany(c => c.Timeline.Commands.AsValueEnumerable())))
        {
            if (segmentEnd <= command.StartTime || command.EndTime <= segmentStart) continue;

            var startTime = float.Clamp(command.StartTime, segmentStart, segmentEnd);
            var endTime = float.Clamp(command.EndTime, segmentStart, segmentEnd);

            switch (command)
            {
                case MoveCommand moveCommand:
                    segmentSprite.Move(moveCommand.Easing,
                        startTime,
                        endTime,
                        moveCommand.ValueAtTime(startTime),
                        moveCommand.ValueAtTime(endTime));

                    break;

                case MoveXCommand moveXCommand:
                    segmentSprite.MoveX(moveXCommand.Easing,
                        startTime,
                        endTime,
                        moveXCommand.ValueAtTime(startTime),
                        moveXCommand.ValueAtTime(endTime));

                    break;

                case MoveYCommand moveYCommand:
                    segmentSprite.MoveY(moveYCommand.Easing,
                        startTime,
                        endTime,
                        moveYCommand.ValueAtTime(startTime),
                        moveYCommand.ValueAtTime(endTime));

                    break;

                case ScaleCommand scaleCommand:
                    segmentSprite.Scale(scaleCommand.Easing,
                        startTime,
                        endTime,
                        scaleCommand.ValueAtTime(startTime),
                        scaleCommand.ValueAtTime(endTime));

                    break;

                case VScaleCommand scaleVecCommand:
                    segmentSprite.ScaleVec(scaleVecCommand.Easing,
                        startTime,
                        endTime,
                        scaleVecCommand.ValueAtTime(startTime),
                        scaleVecCommand.ValueAtTime(endTime));

                    break;

                case RotateCommand rotateCommand:
                    segmentSprite.Rotate(rotateCommand.Easing,
                        startTime,
                        endTime,
                        rotateCommand.ValueAtTime(startTime),
                        rotateCommand.ValueAtTime(endTime));

                    break;

                case FadeCommand fadeCommand:
                    segmentSprite.Fade(fadeCommand.Easing,
                        startTime,
                        endTime,
                        fadeCommand.ValueAtTime(startTime),
                        fadeCommand.ValueAtTime(endTime));

                    break;

                case ColorCommand colorCommand:
                    segmentSprite.Color(colorCommand.Easing,
                        startTime,
                        endTime,
                        colorCommand.ValueAtTime(startTime),
                        colorCommand.ValueAtTime(endTime));

                    break;

                case ParameterCommand parameterCommand:
                    segmentSprite.Parameter(startTime, endTime, parameterCommand.StartValue);
                    break;

                case LoopCommand loopCommand:
                    if (loopCommand.StartTime < startTime || endTime < loopCommand.EndTime)
                    {
                        var loopStartIndex = (startTime - loopCommand.StartTime) / loopCommand.CommandsDuration;
                        var loopEndIndex = (endTime - loopCommand.StartTime) / loopCommand.CommandsDuration;
                        var loopCount = (int)float.Round(loopEndIndex - loopStartIndex);

                        var loopSegmentStartTime = loopCommand.StartTime +
                            loopCommand.CommandsDuration * loopStartIndex;

                        Debug.Assert(loopCount > 0);
                        if (loopCount == 1)
                            foreach (var c in loopCommand.Commands)
                                segmentSprite.AddCommand(c, loopSegmentStartTime);
                        else
                        {
                            segmentSprite.StartLoopGroup(loopSegmentStartTime, loopCount);
                            foreach (var c in loopCommand.Commands) segmentSprite.AddCommand(c);
                            segmentSprite.EndGroup();
                        }
                    }
                    else
                    {
                        segmentSprite.StartLoopGroup(loopCommand.StartTime, loopCommand.LoopCount);
                        foreach (var c in loopCommand.Commands) segmentSprite.AddCommand(c);
                        segmentSprite.EndGroup();
                    }

                    break;

                default: throw new NotSupportedException(command.GetType().FullName);
            }
        }
    }

    static void transferStartState(OsbSprite sprite, OsbSprite segmentSprite, float segmentStart)
    {
        if (sprite.MoveXTimeline.HasCommands || sprite.MoveYTimeline.HasCommands)
        {
            var spriteStartPosition = sprite.PositionAt(segmentStart);
            var segmentStartPosition = segmentSprite.PositionAt(segmentStart);
            if (segmentStartPosition.X != spriteStartPosition.X)
                segmentSprite.MoveX(segmentStart, spriteStartPosition.X);

            if (segmentStartPosition.Y != spriteStartPosition.Y)
                segmentSprite.MoveY(segmentStart, spriteStartPosition.Y);
        }
        else
        {
            var spriteStartPosition = sprite.PositionAt(segmentStart);
            if (segmentSprite.PositionAt(segmentStart) != spriteStartPosition)
                segmentSprite.Move(segmentStart, spriteStartPosition);
        }

        if (sprite.ScaleVecTimeline.HasCommands)
        {
            var spriteStartScale = sprite.ScaleAt(segmentStart);
            if (segmentSprite.ScaleAt(segmentStart) != spriteStartScale)
                segmentSprite.ScaleVec(segmentStart, spriteStartScale.X, spriteStartScale.Y);
        }
        else
        {
            var spriteStartScale = sprite.ScaleAt(segmentStart);
            if (segmentSprite.ScaleAt(segmentStart) != spriteStartScale)
                segmentSprite.Scale(segmentStart, spriteStartScale.X);
        }

        var spriteStartRotation = sprite.RotationAt(segmentStart);
        if (segmentSprite.RotationAt(segmentStart) != spriteStartRotation)
            segmentSprite.Rotate(segmentStart, spriteStartRotation);

        var spriteStartFade = sprite.OpacityAt(segmentStart);
        if (segmentSprite.OpacityAt(segmentStart) != spriteStartFade) segmentSprite.Fade(segmentStart, spriteStartFade);

        var spriteStartColor = sprite.ColorAt(segmentStart);
        if (segmentSprite.ColorAt(segmentStart) != spriteStartColor)
            segmentSprite.Color(segmentStart, spriteStartColor);

        if (segmentSprite.AdditiveAt(segmentStart) != sprite.AdditiveAt(segmentStart))
            segmentSprite.Additive(segmentStart);

        if (segmentSprite.FlipHAt(segmentStart) != sprite.FlipHAt(segmentStart)) segmentSprite.FlipH(segmentStart);

        if (segmentSprite.FlipVAt(segmentStart) != sprite.FlipVAt(segmentStart)) segmentSprite.FlipV(segmentStart);
    }

    static bool canFragmentCommandsAt(OsbSprite sprite, float time)
    {
        using var duringCommandTypes = TempHashSet.Create<Type>();
        foreach (var command in sprite.commandGroups.AsValueEnumerable()
            .Concat(sprite.displayValueBuilders.AsValueEnumerable()
                .SelectMany(c => c.Timeline.Commands.AsValueEnumerable())))
        {
            if (time <= command.StartTime || command.EndTime <= time) continue;

            if (!command.IsFragmentableAt(time)) return false;

            if (command is LoopCommand loop)
            {
                var loopTime = (time - loop.StartTime) % loop.CommandsDuration;
                foreach (var loopCommand in loop.Commands)
                {
                    if (loopTime <= loopCommand.StartTime || loopCommand.EndTime <= loopTime) continue;

                    if (!duringCommandTypes.Add(loopCommand.GetType())) return false;
                }
            }
            else if (!duringCommandTypes.Add(command.GetType())) return false;
        }

        return true;
    }

    static TempHashSet<float> getFragmentationTimes(OsbSprite sprite)
    {
        if (sprite is OsbAnimation animation)
            switch (animation.LoopType)
            {
                case OsbLoopType.LoopOnce:
                    var fragTimes = getCommandFragmentationTimes(sprite);
                    fragTimes.RemoveWhere(t => animation.StartTime + animation.FrameDelay * animation.FrameCount > t);
                    fragTimes.Remove(sprite.StartTime);
                    fragTimes.Remove(sprite.EndTime);

                    return fragTimes;

                case OsbLoopType.LoopForever:
                    var result = TempHashSet.Create<float>();
                    var delay = animation.FrameDelay * animation.FrameCount;
                    for (var t = animation.StartTime; t < animation.EndTime - delay; t += delay) result.Add(t);

                    return result;

                default: throw new NotSupportedException(animation.LoopType.ToString());
            }

        var fragmentationTimes = getCommandFragmentationTimes(sprite);
        fragmentationTimes.Remove(sprite.StartTime);
        fragmentationTimes.Remove(sprite.EndTime);

        return fragmentationTimes;
    }

    static TempHashSet<float> getCommandFragmentationTimes(OsbSprite sprite)
    {
        var set = TempHashSet.Create<float>();
        foreach (var command in sprite.commandGroups.AsValueEnumerable()
            .Concat(sprite.displayValueBuilders.AsValueEnumerable()
                .SelectMany(c => c.Timeline.Commands.AsValueEnumerable())))
        {
            set.Add(command.StartTime);
            if (command is LoopCommand loopCommand)
                for (var i = 1; i < loopCommand.LoopCount - 1; i++)
                    set.Add(command.StartTime + i * loopCommand.CommandsEndTime);

            set.Add(command.EndTime);
        }

        return set;
    }
}