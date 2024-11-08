namespace BrewLib.Audio;

using System;

public static class SoundUtil
{
    public const int C = 40;
    public const int D = C + 2;
    public const int E = D + 2;
    public const int F = E + 1;
    public const int G = F + 2;
    public const int A = G + 2;
    public const int B = A + 2;

    public static float FromLinearVolume(float volume) => MathF.Pow(volume, 4);
    public static float GetNoteFrequency(float note, float a = 440) => MathF.Pow(2, (note - 49) / 12) * a;

    public static float GetNoteRailsback(float note, float factor = .4f)
    {
        var p = (note - 44) / 44;
        return p >= 0 ? note + p * p * factor : note + p * p * -factor;
    }

    public static float SquareWave(float t, float period = .5f) => t % 2 < period ? 1 : -1;
    public static float SawWave(float t) => t % 2 - 1;
    public static float SineWave(float t) => MathF.Sin(t * MathF.Tau);
    public static float TriangleWave(float t) => Math.Abs((t * 4 - 1) % 4 - 2) - 1;
}