using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

    public static void ForEach<T>(this List<T> list, Action<T> action, Func<T, bool> condition)
    {
        for (var i = 0; i < list.Count; ++i)
        {
            var item = list[i];
            if (condition(item)) action(item);
        }
    }
    public static void ForEach<T>(this T[] array, Action<T> action, Func<T, bool> condition)
    {
        for (var i = 0; i < array.Length; ++i)
        {
            var item = array[i];
            if (condition(item)) action(item);
        }
    }
    public static void ForEachUnsafe<T>(this List<T> list, Action<T> action)
    {
        for (var i = 0; i < list.Count; ++i) action(list[i]);
    }
    public static void ForEachUnsafe<T>(this T[] array, Action<T> action)
    {
        for (var i = 0; i < array.Length; ++i) action(array[i]);
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