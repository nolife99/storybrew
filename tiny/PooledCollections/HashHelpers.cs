// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Tiny.PooledCollections;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

static class HashHelpers
{
    public const int HashCollisionThreshold = 100;
    public const int MaxPrimeArrayLength = 0x7FEFFFFD;

    public const int HashPrime = 101;

    public static readonly int[] primes =
    [
        1,
        3,
        7,
        11,
        17,
        23,
        29,
        37,
        47,
        59,
        71,
        89,
        107,
        131,
        163,
        197,
        239,
        293,
        353,
        431,
        521,
        631,
        761,
        919,
        1103,
        1327,
        1597,
        1931,
        2333,
        2801,
        3371,
        4049,
        4861,
        5839,
        7013,
        8419,
        10103,
        12143,
        14591,
        17519,
        21023,
        25229,
        30293,
        36353,
        43627,
        52361,
        62851,
        75431,
        90523,
        108631,
        130363,
        156437,
        187751,
        225307,
        270371,
        324449,
        389357,
        467237,
        560689,
        672827,
        807403,
        968897,
        1162687,
        1395263,
        1674319,
        2009191,
        2411033,
        2893249,
        3471899,
        4166287,
        4999559,
        5999471,
        7199369
    ];

    public static int GetNonRandomizedHashCode(ReadOnlySpan<char> chars)
    {
        ref var src = ref MemoryMarshal.GetReference(chars);

        uint hash1 = (5381 << 16) + 5381;
        var hash2 = hash1;

        ref var ptr = ref Unsafe.As<char, uint>(ref src);
        var length = chars.Length;

        while (length > 2)
        {
            length -= 4;

            hash1 = BitOperations.RotateLeft(hash1, 5) + hash1 ^ ptr;
            hash2 = BitOperations.RotateLeft(hash2, 5) + hash2 ^ Unsafe.Add(ref ptr, 1);
            ptr = ref Unsafe.AddByteOffset(ref ptr, 2);
        }

        if (length > 0) hash2 = BitOperations.RotateLeft(hash2, 5) + hash2 ^ ptr;

        return (int)(hash1 + hash2 * 1566083941);
    }

    public static bool IsPrime(int candidate)
    {
        if ((candidate & 1) != 0)
        {
            var limit = (int)Math.Sqrt(candidate);
            for (var divisor = 3; divisor <= limit; divisor += 2)
                if (candidate % divisor == 0)
                    return false;

            return true;
        }

        return candidate == 2;
    }

    public static int GetPrime(int min)
    {
        if (min < 0) throw new ArgumentException("Cannot get the next prime from a negative number.");

        var primes = HashHelpers.primes;
        for (var i = 0; i < primes.Length; i++)
        {
            var prime = primes[i];
            if (prime >= min) return prime;
        }

        for (var i = min | 1; i < int.MaxValue; i += 2)
            if (IsPrime(i) && (i - 1) % HashPrime != 0)
                return i;

        return min;
    }

    public static int ExpandPrime(int oldSize)
    {
        var newSize = 2 * oldSize;

        if ((uint)newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize) return MaxPrimeArrayLength;

        return GetPrime(newSize);
    }

    public static ulong GetFastModMultiplier(uint divisor) => ulong.MaxValue / divisor + 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FastMod(uint value, uint divisor, ulong multiplier)
    {
        var highbits = (uint)(((multiplier * value >> 32) + 1) * divisor >> 32);
        return highbits;
    }
}