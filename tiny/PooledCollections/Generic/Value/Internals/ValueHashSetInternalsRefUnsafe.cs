namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly struct ValueHashSetInternalsRefUnsafe<T>
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
    internal ValueHashSetInternalsRefUnsafe(scoped ref readonly ValueHashSet<T> source)
    {
#if TARGET_64BIT || PLATFORM_ARCH_64 || UNITY_64
        FastModMultiplier = source._fastModMultiplier;
#endif

        Count = source._count;
        FreeList = source._freeList;
        FreeCount = source._freeCount;
        Version = source._version;
        ClearEntries = ValueHashSet<T>.s_clearEntries;
        Buckets = source._buckets;
        Entries = source._entries;
        Comparer = source._comparer;
    }
}

partial class CollectionInternals
{
    public static ValueHashSetInternalsRefUnsafe<T> GetUnsafeRef<T>(this scoped ref readonly ValueHashSet<T> source)
        => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<Entry<T>> AsSpan<T>(this scoped ref readonly ValueHashSet<T> source)
        => MemoryMarshal.CreateSpan(ref MemoryMarshal.GetArrayDataReference(source._entries), source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Memory<Entry<T>> AsMemory<T>(this scoped ref readonly ValueHashSet<T> source)
        => new(source._entries, 0, source._count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetUnsafe<T>(this scoped ref readonly ValueHashSet<T> source,
        out Entry<T>[] entries,
        out int count)
    {
        entries = source._entries;
        count = source._count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetValueRefOrNullRef<T>(this scoped ref ValueHashSet<T> set, T equalValue) where T : notnull
        => ref set.FindValue(equalValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AddIfNotPresent<T>(this scoped ref ValueHashSet<T> set, T value, out int location)
        => set.AddIfNotPresent(value, out location);
}