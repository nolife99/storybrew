﻿namespace StorybrewEditor.Storyboarding;

using System.Collections.Generic;
using StorybrewCommon.Storyboarding.CommandValues;

public class FrameStats
{
    public float GpuPixelsFrame;
    public bool LastBlendingMode, IncompatibleCommands, OverlappedCommands;
    public string LastTexture;
    public HashSet<string> LoadedPaths = [];
    public CommandDecimal ScreenFill;

    public int SpriteCount, Batches, CommandCount, EffectiveCommandCount;
    public float GpuMemoryFrameMb => GpuPixelsFrame / 1024 / 1024 * 4;
}