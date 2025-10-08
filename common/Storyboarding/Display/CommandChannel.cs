namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;

class CommandChannel<TValue> where TValue : struct, ICommandValue<TValue>
{
    protected readonly List<Command<TValue>> commands = [];
    readonly List<float> times = [];

    public bool HasOverlap;

    public ReadOnlySpan<Command<TValue>> Commands => CollectionsMarshal.AsSpan(commands);

    public bool Add(Command<TValue> command)
    {
        var c = CollectionsMarshal.AsSpan(commands);

        var index = c.BinarySearch(command);
        if (index >= 0)
        {
            c[index] = command;
            return false;
        }

        index = ~index;
        while (index < c.Length)
        {
            if (c[index].CompareTo(command) > 0) break;

            ++index;
        }

        HasOverlap |= index > 0 && command.startTime < c[index - 1].endTime ||
            index < c.Length && c[index].startTime < command.endTime;

        commands.Insert(index, command);
        times.Insert(index, command.startTime);

        return true;
    }

    protected Command<TValue> CommandAtTime(float time)
    {
        var t = Unsafe.As<ListDebugView<float>>(times);
        if (t.Count == 0) return null;

        if (!findCommandIndex(t, time, out var index) && index > 0) --index;

        if (HasOverlap)
        {
            var items = t.Items;
            for (var i = 0; i < index; ++i)
                if (items[i] <= items[index] && time <= commands[i].endTime)
                {
                    index = i;
                    break;
                }
        }

        return commands[index];
    }

    public virtual bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        var command = CommandAtTime(time);
        if (command is null)
        {
            result = default;
            return false;
        }

        result = new(command);
        return true;
    }

    static bool findCommandIndex(ListDebugView<float> c, float time, out int index)
    {
        var len = c.Count;
        var i = 0;

        ref var first = ref MemoryMarshal.GetArrayDataReference(c.Items);

        if (Vector512.IsHardwareAccelerated && len >= Vector512<float>.Count)
        {
            var vTime = Vector512.Create(time);
            while (i + Vector512<float>.Count <= len)
            {
                var mask = Vector512.LessThan(Vector512.LoadUnsafe(ref first, (nuint)i), vTime)
                    .ExtractMostSignificantBits();

                if (mask != 0xFFFF)
                {
                    index = i + BitOperations.TrailingZeroCount(~mask & 0xFFFF);
                    return index < len && Unsafe.Add(ref first, index) == time;
                }

                i += Vector512<float>.Count;
            }
        }

        if (Vector256.IsHardwareAccelerated && len - i >= Vector256<float>.Count)
        {
            var vTime = Vector256.Create(time);
            while (i + Vector256<float>.Count <= len)
            {
                var mask = Vector256.LessThan(Vector256.LoadUnsafe(ref first, (nuint)i), vTime)
                    .ExtractMostSignificantBits();

                if (mask != 0xFF)
                {
                    index = i + BitOperations.TrailingZeroCount(~mask & 0xFF);
                    return index < len && Unsafe.Add(ref first, index) == time;
                }

                i += Vector256<float>.Count;
            }
        }

        if (Vector128.IsHardwareAccelerated && len - i >= Vector128<float>.Count)
        {
            var vTime = Vector128.Create(time);
            while (i + Vector128<float>.Count <= len)
            {
                var mask = Vector128.LessThan(Vector128.LoadUnsafe(ref first, (nuint)i), vTime)
                    .ExtractMostSignificantBits();

                if (mask != 0xF)
                {
                    index = i + BitOperations.TrailingZeroCount(~mask & 0xF);
                    return index < len && Unsafe.Add(ref first, index) == time;
                }

                i += Vector128<float>.Count;
            }
        }

        ref var end = ref Unsafe.Add(ref first, len);
        ref var start = ref first;

        first = ref Unsafe.Add(ref first, i);
        while (Unsafe.IsAddressLessThan(ref first, ref end))
        {
            if (first >= time)
            {
                index = (int)(Unsafe.ByteOffset(ref start, ref first) / sizeof(float));
                return first == time;
            }

            first = ref Unsafe.Add(ref first, 1);
        }

        index = len;
        return false;
    }

    sealed class ListDebugView<T>
    {
        public readonly int Count;
        public readonly T[] Items;
    }
}