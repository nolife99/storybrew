using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BrewLib.Util;

public static class ListExtensions
{
    public static void Move<T>(this List<T> list, int from, int to)
    {
        if (from == to) return;

        var span = CollectionsMarshal.AsSpan(list);
        var item = span[from];

        if (from < to) for (var i = from; i < to; ++i) span[i] = span[i + 1];
        else for (var i = from; i > to; --i) span[i] = span[i - 1];
        span[to] = item;
    }

    public static void ForEach<T>(this List<T> list, Action<T> action, Func<T, bool> condition)
    {
        ref var r0 = ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(list));
        ref var rEnd = ref Unsafe.Add(ref r0, list.Count);

        while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
        {
            if (condition(r0)) action(r0);
            r0 = ref Unsafe.Add(ref r0, 1);
        }
    }
    public static void ForEach<T>(this T[] array, Action<T> action, Func<T, bool> condition)
    {
        ref var r0 = ref MemoryMarshal.GetArrayDataReference(array);
        ref var rEnd = ref Unsafe.Add(ref r0, array.Length);

        while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
        {
            if (condition(r0)) action(r0);
            r0 = ref Unsafe.Add(ref r0, 1);
        }
    }
    public static void ForEachUnsafe<T>(this List<T> list, Action<T> action)
    {
        ref var r0 = ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(list));
        ref var rEnd = ref Unsafe.Add(ref r0, list.Count);

        while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
        {
            action(r0);
            r0 = ref Unsafe.Add(ref r0, 1);
        }
    }
    public static void ForEachUnsafe<T>(this T[] array, Action<T> action)
    {
        ref var r0 = ref MemoryMarshal.GetArrayDataReference(array);
        ref var rEnd = ref Unsafe.Add(ref r0, array.Length);

        while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
        {
            action(r0);
            r0 = ref Unsafe.Add(ref r0, 1);
        }
    }

    public static void Dispose<TKey, TValue>(this IDictionary<TKey, TValue> disposable) where TValue : IDisposable
    {
        foreach (var reference in disposable) try
        {
            reference.Value?.Dispose();
        }
        catch {}

        disposable.Clear();
    }
}