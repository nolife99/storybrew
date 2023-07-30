namespace BrewLib.Util
{
    using System;
    using System.Numerics;

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
            if (from == 0) return to;
            if (to == 0) return -from;
            if (Math.Abs(from - to) == Math.Abs(to - from)) return to - from;

            return -Math.Atan2(Math.Sin(to - from), Math.Cos(to - from));
        }

        public static float DotLength(this Quaternion q1) => Quaternion.Dot(q1, q1);
    }
}