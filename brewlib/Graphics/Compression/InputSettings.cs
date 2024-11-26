namespace BrewLib.Graphics.Compression;

public sealed record LosslessInputSettings(int OptimizationLevel, string CustomInputArgs = "");
public sealed record LossyInputSettings(int MinQuality, int MaxQuality, int Speed, string CustomInputArgs = "");