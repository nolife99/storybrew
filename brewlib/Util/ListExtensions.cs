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
        try
        {
            for (var i = 0; i < span.Length; ++i) if (condition(span[i])) action(span[i]);
        }
        catch
        {
            return false;
        }
        return true;
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