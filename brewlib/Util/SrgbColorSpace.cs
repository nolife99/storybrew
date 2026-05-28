namespace BrewLib.Util;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

static class SrgbColorSpace
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ToLinear(Vector4 color)
        => new(toLinear(color.X), toLinear(color.Y), toLinear(color.Z), color.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float toLinear(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value <= 0.04045f
            ? value / 12.92f
            : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }
}