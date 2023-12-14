using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BrewLib.Util;

public static class ListExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Move<T>(this List<T> list, int from, int to)
    {
        if (from == to) return;

        var item = list[from];
        if (from < to) for (var index = from; index < to; ++index) list[index] = list[index + 1];
        else for (var index = from; index > to; --index) list[index] = list[index - 1];
        list[to] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ForEach<T>(this List<T> list, Action<T> action, Func<T, bool> condition)
    {
        var span = CollectionsMarshal.AsSpan(list);
        ref var r0 = ref MemoryMarshal.GetReference(CollectionsMarshal.AsSpan(list));
        ref var rEnd = ref Unsafe.Add(ref r0, span.Length);

        try
        {
            while (Unsafe.IsAddressLessThan(ref r0, ref rEnd))
            {
                if (condition(r0)) action(r0);
                r0 = ref Unsafe.Add(ref r0, 1);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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