namespace BrewLib.Graphics.Compression;

using System;

public sealed record LosslessInputSettings(int level, string CustomInputArgs = "")
{
    public readonly int OptimizationLevel = Math.Clamp(level, 0, 7);
}

public sealed record LossyInputSettings(int min, int max, int speed, string CustomInputArgs = "")
{
    public readonly int MinQuality = Math.Clamp(min, 0, 100), MaxQuality = Math.Clamp(max, 0, 100),
        Speed = Math.Clamp(speed, 1, 11);
}