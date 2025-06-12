namespace StorybrewEditor.Storyboarding;

using System.Collections.Generic;
using StorybrewCommon.Storyboarding;

public class FrameStats
{
    public readonly HashSet<int> LoadedPaths = [];
    public readonly List<OsbSprite> OverlappedSprites = [], IncompatibleSprites = [], ProlongedSprites = [];
    public float GpuPixelsFrame, ScreenFill;
    public bool LastBlendingMode;

    public int LastTexture, SpriteCount, Batches, CommandCount, EffectiveCommandCount;
    public float GpuMemoryFrameMb => GpuPixelsFrame / 1024 / 1024 * 4;
}