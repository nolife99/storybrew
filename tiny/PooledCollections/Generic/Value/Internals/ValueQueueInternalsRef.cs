namespace Tiny.PooledCollections.Generic.Value.Internals;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public readonly ref struct ValueQueueInternalsRef<T>
{
    public readonly int Head, Tail, Size, Version;
    public readonly bool ClearArray;
    public readonly ReadOnlySpan<T> Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueQueueInternalsRef(scoped ref readonly ValueQueue<T> source)
    {
        Head = source._head;
        Tail = source._tail;
        Size = source._size;
        Version = source._version;
        ClearArray = ValueQueue<T>.s_clearArray;
        Array = source._array;
    }
}

partial class CollectionInternals
{
    public static ValueQueueInternalsRef<T> GetRef<T>(this scoped ref readonly ValueQueue<T> source) => new(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this scoped ref readonly ValueQueue<T> source,
        out int head,
        out int tail)
    {
        head = source._head;
        tail = source._tail;
        return MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetArrayDataReference(source._array), source._size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> AsReadOnlyMemory<T>(this scoped ref readonly ValueQueue<T> source,
        out int head,
        out int tail)
    {
        head = source._head;
        tail = source._tail;
        return new(source._array, 0, source._size);
    }
}