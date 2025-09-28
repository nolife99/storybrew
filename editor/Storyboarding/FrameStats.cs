namespace StorybrewEditor.Storyboarding;

using System.Collections.Generic;
using StorybrewCommon.Storyboarding;

public class FrameStats
{
    public readonly HashSet<string> LoadedPaths = [];
    public readonly List<OsbSprite> OverlappedSprites = [], IncompatibleSprites = [], ProlongedSprites = [];
    public long GpuPixelsFrame;
    public bool LastBlendingMode;
    public string LastTexture;
    public float ScreenFill;

    public int SpriteCount, Batches, CommandCount, EffectiveCommandCount;
}