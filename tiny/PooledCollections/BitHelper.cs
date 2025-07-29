// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Tiny.PooledCollections;

using System;

readonly ref struct BitHelper
{
    const int IntSize = sizeof(int) * 8;
    readonly Span<int> _span;

    public BitHelper(Span<int> span, bool clear)
    {
        if (clear) span.Clear();
        _span = span;
    }

    public void MarkBit(int bitPosition)
    {
        var bitArrayIndex = bitPosition / IntSize;
        var span = _span;

        if ((uint)bitArrayIndex < (uint)span.Length) span[bitArrayIndex] |= 1 << bitPosition % IntSize;
    }

    public bool IsMarked(int bitPosition)
    {
        var bitArrayIndex = bitPosition / IntSize;
        var span = _span;

        return (uint)bitArrayIndex < (uint)span.Length && (span[bitArrayIndex] & 1 << bitPosition % IntSize) != 0;
    }

    public int FindFirstUnmarked(int startPosition = 0)
    {
        var i = startPosition;
        var span = _span;

        for (var bi = i / IntSize; (uint)bi < (uint)span.Length; bi = ++i / IntSize)
            if ((span[bi] & 1 << i % IntSize) == 0)
                return i;

        return -1;
    }

    public int FindFirstMarked(int startPosition = 0)
    {
        var i = startPosition;
        var span = _span;

        for (var bi = i / IntSize; (uint)bi < (uint)span.Length; bi = ++i / IntSize)
            if ((span[bi] & 1 << i % IntSize) != 0)
                return i;

        return -1;
    }

    public static int ToIntArrayLength(int n) => n > 0 ? (n - 1) / IntSize + 1 : 0;
}