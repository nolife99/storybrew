namespace BrewLib.Util
{
    using System;
    public static class MathUtil
    {
        public static bool FloatEquals(float a, float b, float epsilon) => Math.Abs(a - b) < epsilon;
        public static bool DoubleEquals(double a, double b, double epsilon) => Math.Abs(a - b) < epsilon;

        public static int NextPowerOfTwo(int v)
        {
            v--;
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

            var diff = (to - from) % (Math.PI * 2);
            return 2 * diff % (Math.PI * 2) - diff;
        }

        [Obsolete("This method is slower than Math.Sqrt() and shouldn't be used.")]
        public static float FastSqrt(float x)
        {
            var half = x * .5f;
            unsafe
            {
                var i = *(int*)&x;
                i = 0x5f375a86 - (i >> 1);
                x = *(float*)&i;
            }
            for (var j = 0; j < 3; j++) x *= 1.5f - half * x * x;
            return 1 / x;
        }

        [Obsolete("This method is slower than Math.Sqrt() and shouldn't be used.")]
        public static double FastSqrt(double x)
        {
            var half = x * .5;
            unsafe
            {
                var i = *(long*)&x;
                i = 0x5fe6eb50c7b537a9 - (i >> 1);
                x = *(double*)&i;
            }
            for (var j = 0; j < 5; j++) x *= 1.5 - half * x * x;
            return 1 / x;
        }
    }
}