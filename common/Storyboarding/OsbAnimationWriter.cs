namespace StorybrewCommon.Storyboarding;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Commands;
using CommandValues;
using Display;

/// <summary> Base class for writing and exporting an <see cref="OsbAnimation"/>. </summary>
public sealed class OsbAnimationWriter(OsbAnimation animation,
    AnimatedValue<CommandPosition> move,
    AnimatedValue<CommandDecimal> moveX,
    AnimatedValue<CommandDecimal> moveY,
    AnimatedValue<CommandDecimal> scale,
    AnimatedValue<CommandScale> scaleVec,
    AnimatedValue<CommandDecimal> rotate,
    AnimatedValue<CommandDecimal> fade,
    AnimatedValue<CommandColor> color,
    TextWriter writer,
    ExportSettings exportSettings,
    OsbLayer layer) : OsbSpriteWriter(animation,
    move,
    moveX,
    moveY,
    scale,
    scaleVec,
    rotate,
    fade,
    color,
    writer,
    exportSettings,
    layer)
{
    string getLastFramePath() => Path.Combine(Path.GetDirectoryName(animation.TexturePath),
        string.Concat(Path.GetFileNameWithoutExtension(animation.TexturePath),
            (animation.FrameCount - 1).ToString(exportSettings.NumberFormat),
            Path.GetExtension(animation.TexturePath)));

    protected override OsbSprite CreateSprite(ICollection<IFragmentableCommand> segment)
    {
        if (animation.LoopType is OsbLoopType.LoopOnce && segment.Min(c => c.StartTime) >= animation.AnimationEndTime)
        {
            OsbSprite sprite = new()
            {
                InitialPosition = animation.InitialPosition, Origin = animation.Origin, TexturePath = getLastFramePath()
            };

            foreach (var command in segment) sprite.AddCommand(command);
            return sprite;
        }

        OsbAnimation animation1 = new()
        {
            TexturePath = animation.TexturePath,
            InitialPosition = animation.InitialPosition,
            Origin = animation.Origin,
            FrameCount = animation.FrameCount,
            FrameDelay = animation.FrameDelay,
            LoopType = animation.LoopType
        };

        foreach (var command in segment) animation1.AddCommand(command);
        return animation1;
    }

    protected override void WriteHeader(OsbSprite sprite, ref readonly StoryboardTransform transform)
    {
        if (sprite is OsbAnimation animation)
        {
            var frameDelay = animation.FrameDelay;
            writer.Write("Animation");
            WriteHeaderCommon(sprite, in transform);
            writer.WriteLine($",{animation.FrameCount},{frameDelay.ToString(exportSettings.NumberFormat)},{animation.LoopType}");
        }
        else base.WriteHeader(sprite, in transform);
    }

    protected override HashSet<int> GetFragmentationTimes(IEnumerable<IFragmentableCommand> fragCommands)
    {
        var fragmentationTimes = base.GetFragmentationTimes(fragCommands);

        var tMax = fragmentationTimes.Max();
        HashSet<int> nonFragmentableTimes = [];

        for (var d = animation.StartTime; d < animation.AnimationEndTime; d += animation.LoopDuration)
        {
            var range = Enumerable.Range((int)d + 1, (int)(animation.LoopDuration - 1));
            nonFragmentableTimes.UnionWith(range);
        }

        fragmentationTimes.RemoveWhere(t => nonFragmentableTimes.Contains(t) && t < tMax);
        return fragmentationTimes;
    }
}