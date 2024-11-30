namespace StorybrewEditor.Storyboarding;

using System.Collections.Generic;

public class FrameStats
{
    public readonly HashSet<string> LoadedPaths = [];
    public float GpuPixelsFrame, ScreenFill;
    public bool LastBlendingMode, IncompatibleCommands, OverlappedCommands;
    public string LastTexture;

    public int SpriteCount, Batches, CommandCount, EffectiveCommandCount;
    public float GpuMemoryFrameMb => GpuPixelsFrame / 1024 / 1024 * 4;
}