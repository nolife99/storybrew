namespace StorybrewEditor.Storyboarding;

using System.Collections.Generic;
using StorybrewCommon.Storyboarding;

public class FrameStats
{
    public readonly HashSet<string> LoadedPaths = [];
    public float GpuPixelsFrame, ScreenFill;
    public bool LastBlendingMode, IncompatibleCommands;
    public string LastTexture;
    public readonly List<OsbSprite> OverlappedSprites = [];

    public int SpriteCount, Batches, CommandCount, EffectiveCommandCount;
    public float GpuMemoryFrameMb => GpuPixelsFrame / 1024 / 1024 * 4;
}