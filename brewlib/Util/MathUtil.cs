using System;

namespace BrewLib.Util;

public static class MathUtil
{
    public static bool FloatEquals(float a, float b, float epsilon) => Math.Abs(a - b) < epsilon;
    public static bool DoubleEquals(double a, double b, double epsilon) => Math.Abs(a - b) < epsilon;

    public static double ShortestAngleDelta(double from, double to)
    {
        if (from == to) return 0;
        else if (from == 0) return to;
        else if (to == 0) return -from;
        else if (Math.Abs(from) == Math.Abs(to)) return Math.Abs(from) + Math.Abs(to);

        var diff = (to - from) % Math.Tau;
        return 2 * diff % Math.Tau - diff;
    }
}