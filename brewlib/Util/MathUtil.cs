namespace BrewLib.Util;

public static class MathUtil
{
    public static float ShortestAngleDelta(float from, float to)
    {
        if (from == to) return 0;
        if (from == 0) return to;
        if (to == 0) return -from;
        if (float.Abs(from) == float.Abs(to)) return float.Abs(from) + float.Abs(to);

        var diff = (to - from) % float.Tau;
        return 2 * diff % float.Tau - diff;
    }
    public static double ShortestAngleDelta(double from, double to)
    {
        if (from == to) return 0;
        if (from == 0) return to;
        if (to == 0) return -from;
        if (double.Abs(from) == double.Abs(to)) return double.Abs(from) + double.Abs(to);

        var diff = (to - from) % double.Tau;
        return 2 * diff % double.Tau - diff;
    }
}