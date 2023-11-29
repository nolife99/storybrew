using System;

namespace BrewLib.Util
{
    public static class MathUtil
    {
        public static bool FloatEquals(float a, float b, float epsilon) => Math.Abs(a - b) < epsilon;
        public static bool DoubleEquals(double a, double b, double epsilon) => Math.Abs(a - b) < epsilon;

        public static int NextPowerOfTwo(int v)
        {
            --v;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            return v + 1;
        }
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
}