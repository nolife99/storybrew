using System.Collections.Generic;
using System.IO;
using System.Linq;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using StorybrewCommon.Storyboarding.Display;

namespace StorybrewCommon.Storyboarding;

///<summary> Base class for writing and exporting an <see cref="OsbAnimation"/>. </summary>
public class OsbAnimationWriter(OsbAnimation animation,
    AnimatedValue<CommandPosition> move, AnimatedValue<CommandDecimal> moveX, AnimatedValue<CommandDecimal> moveY,
    AnimatedValue<CommandDecimal> scale, AnimatedValue<CommandScale> scaleVec,
    AnimatedValue<CommandDecimal> rotate,
    AnimatedValue<CommandDecimal> fade,
    AnimatedValue<CommandColor> color,
    TextWriter writer, ExportSettings exportSettings, OsbLayer layer) : OsbSpriteWriter(animation,
        move, moveX, moveY,
        scale, scaleVec,
        rotate,
        fade,
        color,
        writer, exportSettings, layer)
{
    readonly OsbAnimation animation = animation;

#pragma warning disable CS1591
    protected override OsbSprite CreateSprite(ICollection<IFragmentableCommand> segment)
    {
        if (animation.LoopType == OsbLoopType.LoopOnce && segment.Min(c => c.StartTime) >= animation.AnimationEndTime)
        {
            OsbSprite sprite = new()
            {
                InitialPosition = animation.InitialPosition,
                Origin = animation.Origin,
                TexturePath = getLastFramePath(),
            };

            foreach (var command in segment) sprite.AddCommand(command);
            return sprite;
        }
        else
        {
            OsbAnimation animation = new()
            {
                TexturePath = this.animation.TexturePath,
                InitialPosition = this.animation.InitialPosition,
                Origin = this.animation.Origin,
                FrameCount = this.animation.FrameCount,
                FrameDelay = this.animation.FrameDelay,
                LoopType = this.animation.LoopType,
            };

            foreach (var command in segment) animation.AddCommand(command);
            return animation;
        }
    }
    protected override void WriteHeader(OsbSprite sprite, StoryboardTransform transform)
    {
        if (sprite is OsbAnimation animation)
        {
            var frameDelay = animation.FrameDelay;
            TextWriter.Write($"Animation");
            WriteHeaderCommon(sprite, transform);
            TextWriter.WriteLine($",{animation.FrameCount},{frameDelay.ToString(ExportSettings.NumberFormat)},{animation.LoopType}");
        }
        else base.WriteHeader(sprite, transform);
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
#pragma warning restore CS1591
    string getLastFramePath()
    {
        var directory = Path.GetDirectoryName(animation.TexturePath);
        var file = string.Concat(Path.GetFileNameWithoutExtension(animation.TexturePath), animation.FrameCount - 1, Path.GetExtension(animation.TexturePath));

        return Path.Combine(directory, file);
    }
}