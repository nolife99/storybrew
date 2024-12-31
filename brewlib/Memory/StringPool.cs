// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// https://github.com/CommunityToolkit/dotnet/blob/main/src/CommunityToolkit.HighPerformance/Buffers/StringPool.cs

namespace BrewLib.Memory;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

public sealed class StringPool
{
    static readonly StringPool Shared = new(4096);
    readonly FixedSizePriorityMap[] maps;
    readonly int numberOfMaps;

    StringPool(int minimumSize)
    {
        minimumSize = int.Max(minimumSize, 128);

        FindFactors(minimumSize, 2, out var x2, out var y2);
        FindFactors(minimumSize, 3, out var x3, out var y3);
        FindFactors(minimumSize, 4, out var x4, out var y4);

        var p2 = x2 * y2;
        var p3 = x3 * y3;
        var p4 = x4 * y4;

        if (p3 < p2)
        {
            p2 = p3;
            x2 = x3;
            y2 = y3;
        }

        if (p4 < p2)
        {
            x2 = x4;
            y2 = y4;
        }

        Span<FixedSizePriorityMap> span = maps = GC.AllocateUninitializedArray<FixedSizePriorityMap>(x2);
        foreach (ref var map in span) map = new(y2);

        numberOfMaps = x2;
        return;

        static void FindFactors(int size, int factor, out int x, out int y)
        {
            var a = MathF.Sqrt((float)size / factor);
            var b = factor * a;

            x = (int)BitOperations.RoundUpToPowerOf2((uint)a);
            y = (int)BitOperations.RoundUpToPowerOf2((uint)b);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetOrAdd(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty) return string.Empty;

        var hashcode = string.GetHashCode(span);
        var bucketIndex = hashcode & Shared.numberOfMaps - 1;

        ref var map = ref DangerousGetReferenceAt(Shared.maps, ref bucketIndex);

        lock (map.SyncRoot) return map.GetOrAdd(span, ref hashcode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGet(ReadOnlySpan<char> span, out string value)
    {
        if (span.IsEmpty)
        {
            value = string.Empty;

            return true;
        }

        var hashcode = string.GetHashCode(span);
        var bucketIndex = hashcode & Shared.numberOfMaps - 1;

        ref var map = ref DangerousGetReferenceAt(Shared.maps, ref bucketIndex);

        lock (map.SyncRoot) return map.TryGet(span, ref hashcode, out value);
    }

    public void Reset()
    {
        foreach (ref var map in maps.AsSpan())
            lock (map.SyncRoot)
                map.Reset();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ref T DangerousGetReferenceAt<T>(T[] span, ref int i)
        => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(span), i);

    record struct FixedSizePriorityMap
    {
        readonly int[] buckets;
        readonly HeapEntry[] heapEntries;
        readonly MapEntry[] mapEntries;

        public readonly Lock SyncRoot = new();

        int count;
        uint timestamp;

        public FixedSizePriorityMap(int capacity)
        {
            buckets = GC.AllocateUninitializedArray<int>(capacity);
            mapEntries = GC.AllocateUninitializedArray<MapEntry>(capacity);
            heapEntries = GC.AllocateUninitializedArray<HeapEntry>(capacity);
            count = 0;
            timestamp = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetOrAdd(ReadOnlySpan<char> span, ref int hashcode)
        {
            ref var result = ref TryGet(span, ref hashcode);

            if (!Unsafe.IsNullRef(ref result)) return result;

            var value = span.ToString();

            Insert(value, ref hashcode);

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(ReadOnlySpan<char> span, ref int hashcode, [NotNullWhen(true)] out string value)
        {
            ref var result = ref TryGet(span, ref hashcode);

            if (!Unsafe.IsNullRef(ref result))
            {
                value = result;

                return true;
            }

            value = null;

            return false;
        }

        public void Reset()
        {
            Array.Clear(buckets);
            Array.Clear(mapEntries);
            Array.Clear(heapEntries);

            count = 0;
            timestamp = 0;
        }

        ref string TryGet(ReadOnlySpan<char> span, ref int hashcode)
        {
            ref var mapEntriesRef = ref MemoryMarshal.GetArrayDataReference(mapEntries);
            ref var entry = ref Unsafe.NullRef<MapEntry>();
            var length = buckets.Length;
            var bucketIndex = hashcode & length - 1;

            for (var i = DangerousGetReferenceAt(buckets, ref bucketIndex) - 1; i < length; i = entry.NextIndex)
            {
                entry = ref Unsafe.Add(ref mapEntriesRef, i);

                if (entry.HashCode != hashcode || !span.SequenceEqual(entry.Value)) continue;
                UpdateTimestamp(ref entry.HeapIndex);

                return ref entry.Value;
            }

            return ref Unsafe.NullRef<string>();
        }

        void Insert(string value, ref int hashcode)
        {
            ref var bucketsRef = ref MemoryMarshal.GetArrayDataReference(buckets);
            ref var mapEntriesRef = ref MemoryMarshal.GetArrayDataReference(mapEntries);
            ref var heapEntriesRef = ref MemoryMarshal.GetArrayDataReference(heapEntries);
            int entryIndex, heapIndex;

            if (count == mapEntries.Length)
            {
                entryIndex = heapEntriesRef.MapIndex;
                heapIndex = 0;

                ref var removedEntry = ref Unsafe.Add(ref mapEntriesRef, entryIndex);
                Remove(ref removedEntry.HashCode, ref entryIndex);
            }
            else
            {
                entryIndex = count;
                heapIndex = count;
            }

            var bucketIndex = hashcode & buckets.Length - 1;
            ref var targetBucket = ref Unsafe.Add(ref bucketsRef, bucketIndex);
            ref var targetMapEntry = ref Unsafe.Add(ref mapEntriesRef, entryIndex);
            ref var targetHeapEntry = ref Unsafe.Add(ref heapEntriesRef, heapIndex);

            targetMapEntry.HashCode = hashcode;
            targetMapEntry.Value = value;
            targetMapEntry.NextIndex = targetBucket - 1;
            targetMapEntry.HeapIndex = heapIndex;

            targetBucket = entryIndex + 1;
            ++count;

            targetHeapEntry.MapIndex = entryIndex;

            UpdateTimestamp(ref targetMapEntry.HeapIndex);
        }

        void Remove(ref int hashcode, ref int mapIndex)
        {
            ref var mapEntriesRef = ref MemoryMarshal.GetArrayDataReference(mapEntries);
            var bucketIndex = hashcode & buckets.Length - 1;
            var entryIndex = DangerousGetReferenceAt(buckets, ref bucketIndex) - 1;
            var lastIndex = -1;

            while (true)
            {
                ref var candidate = ref Unsafe.Add(ref mapEntriesRef, entryIndex);

                if (entryIndex == mapIndex)
                {
                    if (lastIndex != -1)
                    {
                        ref var lastEntry = ref Unsafe.Add(ref mapEntriesRef, lastIndex);

                        lastEntry.NextIndex = candidate.NextIndex;
                    }
                    else DangerousGetReferenceAt(buckets, ref bucketIndex) = candidate.NextIndex + 1;

                    --count;

                    return;
                }

                lastIndex = entryIndex;
                entryIndex = candidate.NextIndex;
            }
        }

        void UpdateTimestamp(ref int heapIndex)
        {
            var currentIndex = heapIndex;
            var c = count;

            ref var mapEntriesRef = ref MemoryMarshal.GetArrayDataReference(mapEntries);
            ref var heapEntriesRef = ref MemoryMarshal.GetArrayDataReference(heapEntries);
            ref var root = ref Unsafe.Add(ref heapEntriesRef, currentIndex);

            var t = timestamp;
            if (t == uint.MaxValue) goto Fallback;

            Downheap:

            root.Timestamp = timestamp = t + 1;

            while (true)
            {
                ref var minimum = ref root;
                var left = currentIndex * 2 + 1;
                var right = currentIndex * 2 + 2;
                var targetIndex = currentIndex;

                if (left < c)
                {
                    ref var child = ref Unsafe.Add(ref heapEntriesRef, left);

                    if (child.Timestamp < minimum.Timestamp)
                    {
                        minimum = ref child;
                        targetIndex = left;
                    }
                }

                if (right < c)
                {
                    ref var child = ref Unsafe.Add(ref heapEntriesRef, right);

                    if (child.Timestamp < minimum.Timestamp)
                    {
                        minimum = ref child;
                        targetIndex = right;
                    }
                }

                if (Unsafe.AreSame(ref root, ref minimum))
                {
                    heapIndex = targetIndex;

                    return;
                }

                Unsafe.Add(ref mapEntriesRef, root.MapIndex).HeapIndex = targetIndex;
                Unsafe.Add(ref mapEntriesRef, minimum.MapIndex).HeapIndex = currentIndex;

                currentIndex = targetIndex;

                (root, minimum) = (minimum, root);
                root = ref Unsafe.Add(ref heapEntriesRef, currentIndex);
            }

            Fallback:

            for (var i = 0; i < count; i++) Unsafe.Add(ref heapEntriesRef, i).Timestamp = (uint)i;
            t = (uint)(c - 1);

            goto Downheap;
        }

        record struct MapEntry
        {
            public int HashCode, HeapIndex, NextIndex;
            public string Value;
        }

        record struct HeapEntry
        {
            public int MapIndex;
            public uint Timestamp;
        }
    }
}