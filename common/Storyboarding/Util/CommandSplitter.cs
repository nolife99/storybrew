namespace StorybrewCommon.Storyboarding.Util;

using System;
using StorybrewCommon.Storyboarding;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding.Display;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;
using ZLinq;

public static class CommandSplitter
{
    public static void Split(OsbSprite sprite, StoryboardSegment segment, int commandSplitThreshold)
    {
        using var sprites = Split(sprite, segment.CreateSprite, segment.CreateAnimation, commandSplitThreshold);
        if (!sprites.Contains(sprite)) segment.Discard(sprite);
    }

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
        using var times = ValueHashSet.Create<float>();
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
        foreach (var command in sprite.MoveTimeline.ExpandedCommandViews) transferPosition(command);

        foreach (var command in sprite.MoveXTimeline.ExpandedCommandViews) transferDecimal(command);
        foreach (var command in sprite.MoveYTimeline.ExpandedCommandViews) transferDecimal(command);
        foreach (var command in sprite.ScaleTimeline.ExpandedCommandViews) transferDecimal(command);
        foreach (var command in sprite.RotateTimeline.ExpandedCommandViews) transferDecimal(command);
        foreach (var command in sprite.FadeTimeline.ExpandedCommandViews) transferDecimal(command);

        foreach (var command in sprite.ScaleVecTimeline.ExpandedCommandViews) transferScale(command);
        foreach (var command in sprite.ColorTimeline.ExpandedCommandViews) transferColor(command);

        foreach (var command in sprite.AdditiveTimeline.ExpandedCommandViews) transferParameter(command);
        foreach (var command in sprite.FlipHTimeline.ExpandedCommandViews) transferParameter(command);
        foreach (var command in sprite.FlipVTimeline.ExpandedCommandViews) transferParameter(command);

        bool intersects(float startTime, float endTime) => segmentStart < endTime && startTime < segmentEnd;

        void transferPosition(CommandView<CommandPosition> command)
        {
            if (!intersects(command.StartTime, command.EndTime)) return;

            var start = float.Clamp(command.StartTime, segmentStart, segmentEnd);
            var end = float.Clamp(command.EndTime, segmentStart, segmentEnd);
            segmentSprite.Move(command.Easing, start, end, command.ValueAtTime(start), command.ValueAtTime(end));
        }

        void transferDecimal(CommandView<CommandDecimal> command)
        {
            if (!intersects(command.StartTime, command.EndTime)) return;

            var start = float.Clamp(command.StartTime, segmentStart, segmentEnd);
            var end = float.Clamp(command.EndTime, segmentStart, segmentEnd);

            switch (command.Kind)
            {
                case CommandKind.MoveX:
                    segmentSprite.MoveX(command.Easing, start, end, command.ValueAtTime(start), command.ValueAtTime(end));
                    break;

                case CommandKind.MoveY:
                    segmentSprite.MoveY(command.Easing, start, end, command.ValueAtTime(start), command.ValueAtTime(end));
                    break;

                case CommandKind.Scale:
                    segmentSprite.Scale(command.Easing, start, end, command.ValueAtTime(start), command.ValueAtTime(end));
                    break;

                case CommandKind.Rotate:
                    segmentSprite.Rotate(command.Easing, start, end, command.ValueAtTime(start), command.ValueAtTime(end));
                    break;

                case CommandKind.Fade:
                    segmentSprite.Fade(command.Easing, start, end, command.ValueAtTime(start), command.ValueAtTime(end));
                    break;
            }
        }

        void transferScale(CommandView<CommandScale> command)
        {
            if (!intersects(command.StartTime, command.EndTime)) return;

            var start = float.Clamp(command.StartTime, segmentStart, segmentEnd);
            var end = float.Clamp(command.EndTime, segmentStart, segmentEnd);
            segmentSprite.ScaleVec(command.Easing, start, end, command.ValueAtTime(start), command.ValueAtTime(end));
        }

        void transferColor(CommandView<CommandColor> command)
        {
            if (!intersects(command.StartTime, command.EndTime)) return;

            var start = float.Clamp(command.StartTime, segmentStart, segmentEnd);
            var end = float.Clamp(command.EndTime, segmentStart, segmentEnd);
            segmentSprite.Color(command.Easing, start, end, command.ValueAtTime(start), command.ValueAtTime(end));
        }

        void transferParameter(CommandView<CommandParameter> command)
        {
            if (!intersects(command.StartTime, command.EndTime)) return;

            var start = float.Clamp(command.StartTime, segmentStart, segmentEnd);
            var end = float.Clamp(command.EndTime, segmentStart, segmentEnd);
            segmentSprite.Parameter(start, end, command.StartValue);
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
        var canFragment = true;
        using var duringCommandTypes = ValueHashSet.Create<CommandKind>();

        foreach (var command in sprite.MoveTimeline.ExpandedCommandViews) check(command);

        foreach (var command in sprite.MoveXTimeline.ExpandedCommandViews) check(command);
        foreach (var command in sprite.MoveYTimeline.ExpandedCommandViews) check(command);
        foreach (var command in sprite.ScaleTimeline.ExpandedCommandViews) check(command);
        foreach (var command in sprite.RotateTimeline.ExpandedCommandViews) check(command);
        foreach (var command in sprite.FadeTimeline.ExpandedCommandViews) check(command);

        foreach (var command in sprite.ScaleVecTimeline.ExpandedCommandViews) check(command);
        foreach (var command in sprite.ColorTimeline.ExpandedCommandViews) check(command);

        foreach (var command in sprite.AdditiveTimeline.ExpandedCommandViews) check(command);
        foreach (var command in sprite.FlipHTimeline.ExpandedCommandViews) check(command);
        foreach (var command in sprite.FlipVTimeline.ExpandedCommandViews) check(command);

        return canFragment;

        void check<TValue>(CommandView<TValue> command) where TValue : struct, ICommandValue<TValue>
        {
            if (!canFragment || time <= command.StartTime || command.EndTime <= time) return;

            if (!command.IsFragmentableAt(time) || !duringCommandTypes.Add(command.Kind)) canFragment = false;
        }
    }

    static ValueHashSet<float> getFragmentationTimes(OsbSprite sprite)
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
                    var result = ValueHashSet.Create<float>();
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

    static ValueHashSet<float> getCommandFragmentationTimes(OsbSprite sprite)
    {
        var set = ValueHashSet.Create<float>();

        foreach (var command in sprite.MoveTimeline.ExpandedCommandViews) add(command);

        foreach (var command in sprite.MoveXTimeline.ExpandedCommandViews) add(command);
        foreach (var command in sprite.MoveYTimeline.ExpandedCommandViews) add(command);
        foreach (var command in sprite.ScaleTimeline.ExpandedCommandViews) add(command);
        foreach (var command in sprite.RotateTimeline.ExpandedCommandViews) add(command);
        foreach (var command in sprite.FadeTimeline.ExpandedCommandViews) add(command);

        foreach (var command in sprite.ScaleVecTimeline.ExpandedCommandViews) add(command);
        foreach (var command in sprite.ColorTimeline.ExpandedCommandViews) add(command);

        foreach (var command in sprite.AdditiveTimeline.ExpandedCommandViews) add(command);
        foreach (var command in sprite.FlipHTimeline.ExpandedCommandViews) add(command);
        foreach (var command in sprite.FlipVTimeline.ExpandedCommandViews) add(command);

        return set;

        void add<TValue>(CommandView<TValue> command) where TValue : struct, ICommandValue<TValue>
        {
            set.Add(command.StartTime);
            set.Add(command.EndTime);
        }
    }

}
