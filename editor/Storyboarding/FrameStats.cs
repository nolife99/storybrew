using System.Collections.Generic;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewEditor.Storyboarding;

public class FrameStats
{
    public string LastTexture;
    public bool LastBlendingMode, IncompatibleCommands, OverlappedCommands;
    public HashSet<string> LoadedPaths = [];

    public int SpriteCount, Batches, CommandCount, EffectiveCommandCount;
    public CommandDecimal ScreenFill;

    public float GpuPixelsFrame;
    public float GpuMemoryFrameMb => GpuPixelsFrame / 1024 / 1024 * 4;
}