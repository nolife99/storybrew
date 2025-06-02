namespace StorybrewEditor.Storyboarding;

using StorybrewCommon.Storyboarding;
using Tiny.PooledCollections.Generic;

public class FrameStats
{
    public readonly PooledHashSet<string> LoadedPaths = [];
    public readonly PooledList<OsbSprite> OverlappedSprites = [], IncompatibleSprites = [], ProlongedSprites = [];
    public float GpuPixelsFrame, ScreenFill;
    public bool LastBlendingMode;
    public string LastTexture = "";

    public int SpriteCount, Batches, CommandCount, EffectiveCommandCount;
    public float GpuMemoryFrameMb => GpuPixelsFrame / 1024 / 1024 * 4;
}