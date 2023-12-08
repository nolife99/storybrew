using System;

namespace BrewLib.Util.Compression;

public sealed class LosslessInputSettings(int level, string args = "")
{
    public readonly string CustomInputArgs = args;
    public readonly int OptimizationLevel = Math.Clamp(level, 0, 7);
}
public sealed class LossyInputSettings(int min, int max, int speed, string args = "")
{
    public readonly string CustomInputArgs = args;
    public readonly int MinQuality = Math.Clamp(min, 0, 100), MaxQuality = Math.Clamp(max, 0, 100), Speed = Math.Clamp(speed, 1, 11);
}