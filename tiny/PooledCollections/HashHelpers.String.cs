// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Tiny.PooledCollections;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

static partial class HashHelpers
{
    // https://github.com/dotnet/runtime/blob/50c3df750a2ad6996159100245645d010c693d87/src/libraries/System.Private.CoreLib/src/System/String.Comparison.cs#L820
    public static int GetNonRandomizedHashCode(string str)
    {
        var chars = str.AsSpan();
        ref var src = ref MemoryMarshal.GetReference(chars);

        uint hash1 = (5381 << 16) + 5381;
        var hash2 = hash1;

        ref var ptr = ref Unsafe.As<char, uint>(ref src);
        var length = chars.Length;

        while (length > 2)
        {
            length -= 4;

            // Where length is 4n-1 (e.g. 3,7,11,15,19) this additionally consumes the null terminator
            hash1 = BitOperations.RotateLeft(hash1, 5) + hash1 ^ ptr;
            hash2 = BitOperations.RotateLeft(hash2, 5) + hash2 ^ Unsafe.Add(ref ptr, 1);
            ptr = ref Unsafe.AddByteOffset(ref ptr, 2);
        }

        if (length > 0)

            // Where length is 4n-3 (e.g. 1,5,9,13,17) this additionally consumes the null terminator
            hash2 = BitOperations.RotateLeft(hash2, 5) + hash2 ^ ptr;

        return (int)(hash1 + hash2 * 1566083941);
    }
}