namespace Tiny.PooledCollections.Generic.Internals;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct HashSetInternalsRefUnsafe<T>
{
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
    public readonly ulong FastModMultiplier;
#endif

    public readonly int Count, FreeList, FreeCount, Version;
    public readonly bool ClearEntries;

    public readonly int[] Buckets;
    public readonly Entry<T>[] Entries;
    public readonly IEqualityComparer<T> Comparer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal HashSetInternalsRefUnsafe(PooledHashSet<T> source)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        FastModMultiplier = source._fastModMultiplier;
#endif

        Count = source._count;
        FreeList = source._freeList;
        FreeCount = source._freeCount;
        Version = source._version;
        ClearEntries = PooledHashSet<T>.s_clearEntries;
        Buckets = source._buckets;
        Entries = source._entries;
        Comparer = source._comparer;
    }
}

partial class CollectionInternals
{
    public static HashSetInternalsRefUnsafe<T> GetUnsafeRef<T>(this PooledHashSet<T> source) => new(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<Entry<T>> AsSpan<T>(this PooledHashSet<T> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._entries), source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<Entry<T>> AsMemory<T>(this PooledHashSet<T> source) => new(source._entries, 0, source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this PooledHashSet<T> source, out Entry<T>[] entries, out int count)
    {
        entries = source._entries;
        count = source._count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetValueRefOrNullRef<T>(this PooledHashSet<T> set, T equalValue) where T : notnull
        => ref set.FindValue(equalValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AddIfNotPresent<T>(this PooledHashSet<T> set, T value, out int location)
        => set.AddIfNotPresent(value, out location);
}