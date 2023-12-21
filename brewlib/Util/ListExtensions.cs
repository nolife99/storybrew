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

        var item = list[from];
        if (from < to) for (var i = from; i < to; ++i) list[i] = list[i + 1];
        else for (var i = from; i > to; --i) list[i] = list[i - 1];
        list[to] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ForEach<T>(this List<T> list, Action<T> action, Func<T, bool> condition)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            list.ForEach(item =>
            {
                if (condition(item)) action(item);
            });
            return;
        }

        ref var r0 = ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(list));
        ref var rEnd = ref Unsafe.Add(ref r0, list.Count);

        while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
        {
            if (condition(r0)) action(r0);
            r0 = ref Unsafe.Add(ref r0, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ForEach<T>(this T[] array, Action<T> action, Func<T, bool> condition)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            for (var i = 0; i < array.Length; ++i)
            {
                var item = array[i];
                if (condition(item)) action(item);
            }
            return;
        }

        ref var r0 = ref MemoryMarshal.GetArrayDataReference(array);
        ref var rEnd = ref Unsafe.Add(ref r0, array.Length);

        while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
        {
            if (condition(r0)) action(r0);
            r0 = ref Unsafe.Add(ref r0, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ForEachUnsafe<T>(this List<T> list, Action<T> action)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            list.ForEach(action);
            return;
        }

        ref var r0 = ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(list));
        ref var rEnd = ref Unsafe.Add(ref r0, list.Count);

        while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
        {
            action(r0);
            r0 = ref Unsafe.Add(ref r0, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ForEachUnsafe<T>(this T[] array, Action<T> action)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            for (var i = 0; i < array.Length; ++i) action(array[i]);
            return;
        }

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