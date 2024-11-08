namespace StorybrewCommon.Storyboarding;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Commands;
using CommandValues;
using Display;

/// <summary> Base class for writing and exporting an <see cref="OsbAnimation" />. </summary>
public class OsbAnimationWriter(
    OsbAnimation animation, AnimatedValue<CommandPosition> move, AnimatedValue<CommandDecimal> moveX,
    AnimatedValue<CommandDecimal> moveY, AnimatedValue<CommandDecimal> scale, AnimatedValue<CommandScale> scaleVec,
    AnimatedValue<CommandDecimal> rotate, AnimatedValue<CommandDecimal> fade, AnimatedValue<CommandColor> color,
    TextWriter writer, ExportSettings exportSettings, OsbLayer layer) : OsbSpriteWriter(animation, move, moveX, moveY,
    scale, scaleVec, rotate, fade, color, writer, exportSettings, layer)
{
    readonly OsbAnimation animation = animation;

    string getLastFramePath()
        => Path.Combine(Path.GetDirectoryName(animation.TexturePath),
            string.Concat(Path.GetFileNameWithoutExtension(animation.TexturePath), animation.FrameCount - 1,
                Path.GetExtension(animation.TexturePath)));

#pragma warning disable CS1591
    protected override OsbSprite CreateSprite(ICollection<IFragmentableCommand> segment)
    {
        if (this.animation.LoopType is OsbLoopType.LoopOnce &&
            segment.Min(c => c.StartTime) >= this.animation.AnimationEndTime)
        {
            OsbSprite sprite = new()
            {
                InitialPosition = this.animation.InitialPosition,
                Origin = this.animation.Origin,
                TexturePath = getLastFramePath()
            };

            foreach (var command in segment) sprite.AddCommand(command);
            return sprite;
        }

        OsbAnimation animation = new()
        {
            TexturePath = this.animation.TexturePath,
            InitialPosition = this.animation.InitialPosition,
            Origin = this.animation.Origin,
            FrameCount = this.animation.FrameCount,
            FrameDelay = this.animation.FrameDelay,
            LoopType = this.animation.LoopType
        };

        foreach (var command in segment) animation.AddCommand(command);
        return animation;
    }

    protected override void WriteHeader(OsbSprite sprite, StoryboardTransform transform)
    {
        if (sprite is OsbAnimation animation)
        {
            var frameDelay = animation.FrameDelay;
            writer.Write("Animation");
            WriteHeaderCommon(sprite, transform);
            writer.WriteLine($",{animation.FrameCount},{frameDelay.ToString(exportSettings.NumberFormat)},{
                animation.LoopType}");
        }
        else
            base.WriteHeader(sprite, transform);
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
}