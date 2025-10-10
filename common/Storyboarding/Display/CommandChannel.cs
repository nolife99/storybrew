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
        var t = CollectionsMarshal.AsSpan(times);
        if (t.IsEmpty) return null;

        if (!findCommandIndex(t, time, out var index) && index > 0) --index;

        if (HasOverlap)
            for (var i = 0; i < index; i++)
                if (t[i] <= t[index] && time <= commands[i].endTime)
                {
                    index = i;
                    break;
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool findCommandIndex(ReadOnlySpan<float> c, float time, out int index)
    {
        var left = 0;
        var right = c.Length - 1;

        if (Vector512.IsHardwareAccelerated && right + 1 > Vector512<float>.Count)
        {
            if (search512(c, time, out var narrowed))
            {
                index = narrowed;
                return true;
            }

            left = Math.Max(0, narrowed - Vector512<float>.Count);
            right = Math.Min(c.Length - 1, narrowed + Vector512<float>.Count);
        }

        if (Vector256.IsHardwareAccelerated && right - left + 1 > Vector256<float>.Count)
        {
            if (search256(c.Slice(left, right - left + 1), time, out var narrowed))
            {
                index = left + narrowed;
                return true;
            }

            left += narrowed;
            right = Math.Min(c.Length - 1, left + Vector256<float>.Count);
        }

        if (Vector128.IsHardwareAccelerated && right - left + 1 > Vector128<float>.Count)
        {
            if (search128(c.Slice(left, right - left + 1), time, out var narrowed))
            {
                index = left + narrowed;
                return true;
            }

            left += narrowed;
            right = Math.Min(c.Length - 1, left + Vector128<float>.Count);
        }

        ref var first = ref MemoryMarshal.GetReference(c);
        while (left <= right)
        {
            var mid = left + right >> 1;
            var v = Unsafe.Add(ref first, mid);

            if (v > time) right = mid - 1;
            else if (v < time) left = mid + 1;
            else
            {
                index = mid;
                return true;
            }
        }

        index = left;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool search512(ReadOnlySpan<float> c, float time, out int index)
    {
        ref var first = ref MemoryMarshal.GetReference(c);
        var left = 0;
        var right = c.Length - 1;

        var V = Vector512<float>.Count;
        var vTime = Vector512.Create(time);

        int len;
        while ((len = right - left + 1) > V)
        {
            var blockStart = left + (len - V >> 1);

            var v = Vector512.LoadUnsafe(ref first, (nuint)blockStart);

            var eqMask = Vector512.Equals(v, vTime).ExtractMostSignificantBits();
            if (eqMask != 0)
            {
                index = blockStart + BitOperations.TrailingZeroCount(eqMask);
                return true;
            }

            var countLess = BitOperations.PopCount(Vector512.LessThan(v, vTime).ExtractMostSignificantBits());
            if (countLess == 0) right = blockStart - 1;
            else if (countLess == V) left = blockStart + V;
            else
            {
                left = blockStart + countLess;
                break;
            }
        }

        index = left;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool search256(ReadOnlySpan<float> c, float time, out int index)
    {
        ref var first = ref MemoryMarshal.GetReference(c);
        var left = 0;
        var right = c.Length - 1;

        var V = Vector256<float>.Count;
        var vTime = Vector256.Create(time);

        int len;
        while ((len = right - left + 1) > V)
        {
            var blockStart = left + (len - V >> 1);

            var v = Vector256.LoadUnsafe(ref first, (nuint)blockStart);

            var eqMask = Vector256.Equals(v, vTime).ExtractMostSignificantBits();
            if (eqMask != 0)
            {
                index = blockStart + BitOperations.TrailingZeroCount(eqMask);
                return true;
            }

            var countLess = BitOperations.PopCount(Vector256.LessThan(v, vTime).ExtractMostSignificantBits());
            if (countLess == 0) right = blockStart - 1;
            else if (countLess == V) left = blockStart + V;
            else
            {
                left = blockStart + countLess;
                break;
            }
        }

        index = left;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool search128(ReadOnlySpan<float> c, float time, out int index)
    {
        ref var first = ref MemoryMarshal.GetReference(c);
        var left = 0;
        var right = c.Length - 1;

        var V = Vector128<float>.Count;
        var vTime = Vector128.Create(time);

        int len;
        while ((len = right - left + 1) > V)
        {
            var blockStart = left + (len - V >> 1);

            var v = Vector128.LoadUnsafe(ref first, (nuint)blockStart);

            var eqMask = Vector128.Equals(v, vTime).ExtractMostSignificantBits();
            if (eqMask != 0)
            {
                index = blockStart + BitOperations.TrailingZeroCount(eqMask);
                return true;
            }

            var countLess = BitOperations.PopCount(Vector128.LessThan(v, vTime).ExtractMostSignificantBits());
            if (countLess == 0) right = blockStart - 1;
            else if (countLess == V) left = blockStart + V;
            else
            {
                left = blockStart + countLess;
                break;
            }
        }

        index = left;
        return false;
    }
}