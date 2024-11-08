namespace BrewLib.Util;

using System;

public static class MathUtil
{
    public static float ShortestAngleDelta(float from, float to)
    {
        if (from == to) return 0;
        if (from == 0) return to;
        if (to == 0) return -from;
        if (Math.Abs(from) == Math.Abs(to)) return Math.Abs(from) + Math.Abs(to);

        var diff = (to - from) % MathF.Tau;
        return 2 * diff % MathF.Tau - diff;
    }
    public static double ShortestAngleDelta(double from, double to)
    {
        if (from == to) return 0;
        if (from == 0) return to;
        if (to == 0) return -from;
        if (Math.Abs(from) == Math.Abs(to)) return Math.Abs(from) + Math.Abs(to);

        var diff = (to - from) % Math.Tau;
        return 2 * diff % Math.Tau - diff;
    }
}