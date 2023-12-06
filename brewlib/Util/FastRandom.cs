using System;
using System.Runtime.CompilerServices;

namespace BrewLib.Util;

///<inheritdoc cref="Random"/>
public class FastRandom
{
    const double UNIT_INT = 4.65661287307739E-10, UNIT_UINT = 2.3283064365387E-10;
    const uint Y = 0x32378FC7, Z = 0xD55F8767, W = 0x104AA1AD;

    uint x, y, z, w;

    ///<summary> Initializes a new instance of the <see cref="FastRandom"/> class, using a time-dependent seed value. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public FastRandom() => Reinitialise(Environment.TickCount);

    ///<summary> Initializes a new instance of the <see cref="FastRandom"/> class, using the specified seed value. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public FastRandom(int seed) => Reinitialise(seed);

    ///<summary> Resets this instance with a new seed value. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void Reinitialise(int seed)
    {
        x = (uint)seed;
        y = Y;
        z = Z;
        w = W;
    }

    ///<inheritdoc cref="Random.Next"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public int Next()
    {
        int rtn;
        do
        {
            var t = x ^ (x << 11);
            x = y;
            y = z;
            z = w;
            w = w ^ (w >> 19) ^ t ^ (t >> 8);

            rtn = (int)(w & 0x7FFFFFFF);
        } while (rtn == 0x7FFFFFFF);

        return rtn;
    }

    ///<inheritdoc cref="Random.Next(int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public int Next(int maxValue)
    {
        var t = x ^ (x << 11);
        x = y;
        y = z;
        z = w;

        return (int)(UNIT_INT * (int)(0x7FFFFFFF & (w = w ^ (w >> 19) ^ t ^ (t >> 8))) * maxValue);
    }

    ///<inheritdoc cref="Random.Next(int, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public int Next(int minValue, int maxValue)
    {
        var t = x ^ (x << 11);
        x = y;
        y = z;
        z = w;

        var range = maxValue - minValue;
        if (range < 0) return minValue + (int)(UNIT_UINT * (w = w ^ (w >> 19) ^ t ^ (t >> 8)) * (maxValue - (long)minValue));
        return minValue + (int)(UNIT_INT * (int)(0x7FFFFFFF & (w = w ^ (w >> 19) ^ t ^ (t >> 8))) * range);
    }

    ///<inheritdoc cref="Random.NextDouble"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public double NextDouble()
    {
        var t = x ^ (x << 11);
        x = y;
        y = z;
        z = w;

        return UNIT_INT * (int)(0x7FFFFFFF & (w = w ^ (w >> 19) ^ t ^ (t >> 8)));
    }

    ///<inheritdoc cref="Random.NextBytes"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void NextBytes(byte[] buffer)
    {
        uint x = this.x, y = this.y, z = this.z, w = this.w, t;
        var i = 0;

        Span<byte> span = new(buffer);
        for (var bound = span.Length - 3; i < bound;)
        {
            t = x ^ (x << 11);
            x = y;
            y = z;
            z = w;
            w = w ^ (w >> 19) ^ t ^ (t >> 8);

            span[i++] = (byte)w;
            span[i++] = (byte)(w >> 8);
            span[i++] = (byte)(w >> 16);
            span[i++] = (byte)(w >> 24);
        }

        while (i < span.Length)
        {
            t = x ^ (x << 11);
            x = y;
            y = z;
            z = w;
            w = w ^ (w >> 19) ^ t ^ (t >> 8);

            span[i++] = (byte)w;
            if (i >= span.Length) break;

            span[i++] = (byte)(w >> 8);
            if (i >= span.Length) break;

            span[i++] = (byte)(w >> 16);
            if (i >= span.Length) break;

            span[i++] = (byte)(w >> 24);
        }

        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    ///<inheritdoc cref="Random.NextBytes"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] NextBytes(int length)
    {
        var buffer = GC.AllocateUninitializedArray<byte>(length);
        NextBytes(buffer);
        return buffer;
    }

    ///<summary> Returns a random unsigned integer. </summary>
    ///<returns> A 32-bit unsigned integer that is ≥ 0 and ≤ <see cref="uint.MaxValue"/>. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public uint NextUInt()
    {
        var t = x ^ (x << 11);
        x = y;
        y = z;
        z = w;
        return w = w ^ (w >> 19) ^ t ^ (t >> 8);
    }

    ///<summary> Returns a non-negative random integer. </summary>
    ///<returns> A 32-bit signed integer that is ≥ 0 and ≤ <see cref="uint.MaxValue"/>. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public int NextInt()
    {
        var t = x ^ (x << 11);
        x = y;
        y = z;
        z = w;
        return (int)(0x7FFFFFFF & (w = w ^ (w >> 19) ^ t ^ (t >> 8)));
    }

    uint bitBuffer, bitMask = 1;

    ///<summary> Returns a random bit. </summary>
    ///<returns> A random bit that is equal to <see langword="true"/> or <see langword="false"/>. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public bool NextBool()
    {
        if (bitMask == 1)
        {
            var t = x ^ (x << 11);
            x = y;
            y = z;
            z = w;
            bitBuffer = w = w ^ (w >> 19) ^ t ^ (t >> 8);

            bitMask = 0x80000000;
            return (bitBuffer & bitMask) == 0;
        }

        return (bitBuffer & (bitMask >>= 1)) == 0;
    }
}