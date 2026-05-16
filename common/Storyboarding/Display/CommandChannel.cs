namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Animations;
using BrewLib.Util;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Temporary.Internals;

class CommandChannel<TValue> where TValue : struct, ICommandValue<TValue>
{
    const ushort EasingMask = 0x7F;
    const ushort MaintainValueMask = 0x80;

    protected readonly List<float> startTimes = [];
    protected readonly List<float> endTimes = [];
    protected readonly List<TValue> startValues = [];
    protected readonly List<TValue> endValues = [];
    protected readonly List<ushort> flags = [];
    protected readonly List<CommandKind> kinds = [];

    public bool HasOverlap;
    public int Count => startTimes.Count;

    public bool Add(CommandKind kind,
        OsbEasing easing,
        float startTime,
        float endTime,
        TValue startValue,
        TValue endValue,
        bool maintainValue = true)
    {
        if (startTime > endTime) endTime = startTime;

        var index = findInsertIndex(startTime, endTime);
        HasOverlap |= index > 0 && startTime < endTimes[index - 1] ||
                      index < Count && startTimes[index] < endTime;

        startTimes.Insert(index, startTime);
        endTimes.Insert(index, endTime);
        startValues.Insert(index, startValue);
        endValues.Insert(index, endValue);
        flags.Insert(index, packFlags(easing, maintainValue));
        kinds.Insert(index, kind);

        return true;
    }

    int findInsertIndex(float startTime, float endTime)
    {
        var lo = 0;
        var hi = Count;

        while (lo < hi)
        {
            var mid = lo + hi >>> 1;
            var midStart = startTimes[mid];

            if (startTime < midStart || startTime == midStart && endTime < endTimes[mid]) hi = mid;
            else lo = mid + 1;
        }

        return lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ushort packFlags(OsbEasing easing, bool maintainValue)
        => (ushort)((byte)easing | (maintainValue ? MaintainValueMask : 0));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float StartTimeAt(int index) => startTimes[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float EndTimeAt(int index) => endTimes[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue StartValueAt(int index) => startValues[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue EndValueAt(int index) => endValues[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OsbEasing EasingAt(int index) => (OsbEasing)(flags[index] & EasingMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CommandKind KindAt(int index) => kinds[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MaintainsValueAt(int index) => (flags[index] & MaintainValueMask) != 0;

    protected int FindIndex(float time)
    {
        var count = Count;
        if (count == 0) return -1;

        var times = CollectionsMarshal.AsSpan(startTimes);

        if (time < times[0]) return 0;

        var last = count - 1;
        if (time >= times[last]) return last;

        if (count < Vector128<float>.Count)
            return times.Length == 3 && time >= times[1] ? 1 : 0;

        return upperBound(times, time) - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int upperBound(ReadOnlySpan<float> times, float time)
    {
        var count = times.Length;

        ref var first = ref MemoryMarshal.GetReference(times);
        if (Vector512.IsHardwareAccelerated && count >= Vector512<float>.Count)
            return upperBoundVector512(ref first, count, time);

        if (Vector256.IsHardwareAccelerated && count >= Vector256<float>.Count)
            return UpperBoundVector256(ref first, count, time);

        return UpperBoundVector128(ref first, count, time);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int UpperBoundVector128(ref float first, int count, float time)
    {
        var lo = 0;
        var hi = count;

        while (hi - lo > Vector128<float>.Count)
        {
            var mid = lo + hi >>> 1;

            if (time < Unsafe.Add(ref first, mid))
                hi = mid;
            else
                lo = mid + 1;
        }

        var blockStart = Math.Max(hi - Vector128<float>.Count, 0);

        var values = Vector128.LoadUnsafe(in first, (nuint)blockStart);
        var needle = Vector128.Create(time);

        var mask = Vector128.LessThanOrEqual(values, needle).ExtractMostSignificantBits();

        return blockStart + BitOperations.PopCount(mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int UpperBoundVector256(ref float first, int count, float time)
    {
        var lo = 0;
        var hi = count;

        while (hi - lo > Vector256<float>.Count)
        {
            var mid = lo + hi >>> 1;

            if (time < Unsafe.Add(ref first, mid))
                hi = mid;
            else
                lo = mid + 1;
        }

        var blockStart = Math.Max(hi - Vector256<float>.Count, 0);

        var values = Vector256.LoadUnsafe(in first, (nuint)blockStart);
        var needle = Vector256.Create(time);

        var mask = Vector256.LessThanOrEqual(values, needle).ExtractMostSignificantBits();

        return blockStart + BitOperations.PopCount(mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int upperBoundVector512(ref float first, int count, float time)
    {
        var lo = 0;
        var hi = count;

        while (hi - lo > Vector512<float>.Count)
        {
            var mid = lo + hi >>> 1;

            if (time < Unsafe.Add(ref first, mid))
                hi = mid;
            else
                lo = mid + 1;
        }

        var blockStart = Math.Max(hi - Vector512<float>.Count, 0);

        var values = Vector512.LoadUnsafe(in first, (nuint)blockStart);
        var needle = Vector512.Create(time);

        var mask = Vector512.LessThanOrEqual(values, needle).ExtractMostSignificantBits();

        return blockStart + BitOperations.PopCount(mask);
    }

    protected int FindIndexSlowOverlap(float time)
    {
        var index = FindIndex(time);
        if (index <= 0) return index;

        for (var i = 0; i < index; i++)
            if (startTimes[i] <= startTimes[index] && time <= endTimes[i])
            {
                index = i;
                break;
            }

        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TValue ValueAtTimeDirect(float time, TValue defaultValue)
    {
        var index = HasOverlap ? FindIndexSlowOverlap(time) : FindIndex(time);
        return index < 0 ? defaultValue : ValueAtIndex(index, time, defaultValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TValue ValueAtIndex(int index, float time, TValue defaultValue = default)
    {
        var startTime = startTimes[index];
        if (time < startTime) return MaintainsValueAt(index) ? startValues[index] : defaultValue;

        var endTime = endTimes[index];
        if (endTime < time) return MaintainsValueAt(index) ? endValues[index] : defaultValue;

        var duration = endTime - startTime;
        return startValues[index] + (endValues[index] - startValues[index]) *
            (duration > 0 ? EasingAt(index).Ease((time - startTime) / duration) : 0);
    }

    public virtual bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        var index = HasOverlap ? FindIndexSlowOverlap(time) : FindIndex(time);
        if (index < 0)
        {
            result = default;
            return false;
        }

        result = new(this, index);
        return true;
    }

    internal ViewEnumerable Views
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(this);
    }

    internal virtual int ViewCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal virtual CommandView<TValue> ViewAt(int viewIndex) => GetView(viewIndex);

    internal CommandView<TValue> GetView(int index, float timeOffset = 0)
        => new(kinds[index], startTimes[index], endTimes[index], timeOffset, EasingAt(index), startValues[index],
            endValues[index]);

    internal void OffsetAll(float offset)
    {
        for (var i = 0; i < Count; i++)
        {
            startTimes[i] += offset;
            endTimes[i] += offset;
        }
    }

    internal float CommandsStartTime
    {
        get
        {
            var result = float.MaxValue;
            for (var i = 0; i < Count; i++) result = float.Min(result, startTimes[i]);
            return result;
        }
    }

    internal float CommandsEndTime
    {
        get
        {
            var result = float.MinValue;
            for (var i = 0; i < Count; i++) result = float.Max(result, endTimes[i]);
            return result;
        }
    }


    internal virtual bool TryGetTimeRange(out float startTime, out float endTime)
    {
        if (Count == 0)
        {
            startTime = endTime = 0;
            return false;
        }

        startTime = startTimes[0];
        endTime = CommandsEndTime;
        return true;
    }
    internal virtual void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        scoped ref readonly StoryboardTransform transform,
        int indentation)
    {
        for (var i = 0; i < Count; i++) writeCommand(writer, exportSettings, in transform, indentation, i);
    }

    protected void writeCommand(TextWriter writer,
        ExportSettings exportSettings,
        scoped ref readonly StoryboardTransform transform,
        int indentation,
        int index)
    {
        writeIndent(writer, indentation);

        var kind = kinds[index];
        writeIdentifier(writer, kind);
        writer.Write(',');
        writeInt(writer, (int)EasingAt(index));
        writer.Write(',');

        var startTime = startTimes[index];
        var endTime = endTimes[index];
        writeTime(writer, exportSettings, startTime);
        writer.Write(',');

        if (!sameExportTime(exportSettings, startTime, endTime))
            writeTime(writer, exportSettings, endTime);

        writer.Write(',');
        writeValues(writer, exportSettings, in transform, index, kind);
        writer.WriteLine();
    }

    protected static void writeIndent(TextWriter writer, int indentation)
    {
        for (var i = 0; i < indentation; i++) writer.Write(' ');
    }

    protected static void writeInt(TextWriter writer, int value)
    {
        Span<char> buffer = stackalloc char[16];
        if (!value.TryFormat(buffer, out var written)) throw new InvalidOperationException("Integer formatting buffer was too small.");
        writer.Write(buffer[..written]);
    }

    protected static void writeTime(TextWriter writer, ExportSettings exportSettings, float time)
    {
        var value = exportSettings.UseFloatForTime ? time : float.Round(time);
        Span<char> buffer = stackalloc char[32];
        if (!value.TryFormat(buffer, out var written, provider: exportSettings.NumberFormat))
            throw new InvalidOperationException("Time formatting buffer was too small.");
        writer.Write(buffer[..written]);
    }

    static bool sameExportTime(ExportSettings exportSettings, float left, float right)
        => exportSettings.UseFloatForTime ? left == right : float.Round(left) == float.Round(right);

    static void writeIdentifier(TextWriter writer, CommandKind kind)
    {
        switch (kind)
        {
            case CommandKind.Move:
                writer.Write('M');
                break;
            case CommandKind.MoveX:
                writer.Write("MX");
                break;
            case CommandKind.MoveY:
                writer.Write("MY");
                break;
            case CommandKind.Scale:
                writer.Write('S');
                break;
            case CommandKind.ScaleVec:
                writer.Write('V');
                break;
            case CommandKind.Rotate:
                writer.Write('R');
                break;
            case CommandKind.Fade:
                writer.Write('F');
                break;
            case CommandKind.Color:
                writer.Write('C');
                break;
            case CommandKind.Additive:
            case CommandKind.FlipH:
            case CommandKind.FlipV:
                writer.Write('P');
                break;
            default:
                throw new NotSupportedException(kind.ToString());
        }
    }

    void writeValues(TextWriter writer,
        ExportSettings exportSettings,
        scoped ref readonly StoryboardTransform transform,
        int index,
        CommandKind kind)
    {
        switch (kind)
        {
            case CommandKind.Move:
            {
                var start = commandValue<TValue, CommandPosition>(startValues[index]);
                var end = commandValue<TValue, CommandPosition>(endValues[index]);
                if (!transform.IsIdentity)
                {
                    start = transform.ApplyToPosition(start);
                    end = transform.ApplyToPosition(end);
                }

                writeCommandValues(writer, exportSettings, start, end);
                break;
            }
            case CommandKind.MoveX:
            {
                var start = commandValue<TValue, CommandDecimal>(startValues[index]);
                var end = commandValue<TValue, CommandDecimal>(endValues[index]);
                if (!transform.IsIdentity)
                {
                    start = transform.ApplyToPositionX(start);
                    end = transform.ApplyToPositionX(end);
                }

                writeCommandValues(writer, exportSettings, start, end);
                break;
            }
            case CommandKind.MoveY:
            {
                var start = commandValue<TValue, CommandDecimal>(startValues[index]);
                var end = commandValue<TValue, CommandDecimal>(endValues[index]);
                if (!transform.IsIdentity)
                {
                    start = transform.ApplyToPositionY(start);
                    end = transform.ApplyToPositionY(end);
                }

                writeCommandValues(writer, exportSettings, start, end);
                break;
            }
            case CommandKind.Scale:
            {
                var start = commandValue<TValue, CommandDecimal>(startValues[index]);
                var end = commandValue<TValue, CommandDecimal>(endValues[index]);
                if (!transform.IsIdentity)
                {
                    start = transform.ApplyToScale(start);
                    end = transform.ApplyToScale(end);
                }

                writeCommandValues(writer, exportSettings, start, end);
                break;
            }
            case CommandKind.ScaleVec:
            {
                var start = commandValue<TValue, CommandScale>(startValues[index]);
                var end = commandValue<TValue, CommandScale>(endValues[index]);
                if (!transform.IsIdentity)
                {
                    start = transform.ApplyToScale(start);
                    end = transform.ApplyToScale(end);
                }

                writeCommandValues(writer, exportSettings, start, end);
                break;
            }
            case CommandKind.Rotate:
            {
                var start = commandValue<TValue, CommandDecimal>(startValues[index]);
                var end = commandValue<TValue, CommandDecimal>(endValues[index]);
                if (!transform.IsIdentity)
                {
                    start = transform.ApplyToRotation(start);
                    end = transform.ApplyToRotation(end);
                }

                writeCommandValues(writer, exportSettings, start, end);
                break;
            }
            case CommandKind.Fade:
                writeCommandValues(writer,
                    exportSettings,
                    commandValue<TValue, CommandDecimal>(startValues[index]),
                    commandValue<TValue, CommandDecimal>(endValues[index]));
                break;
            case CommandKind.Color:
                writeCommandValues(writer,
                    exportSettings,
                    commandValue<TValue, CommandColor>(startValues[index]),
                    commandValue<TValue, CommandColor>(endValues[index]));
                break;
            case CommandKind.Additive:
            case CommandKind.FlipH:
            case CommandKind.FlipV:
                writeCommandValues(writer,
                    exportSettings,
                    commandValue<TValue, CommandParameter>(startValues[index]),
                    commandValue<TValue, CommandParameter>(startValues[index]),
                    false);
                break;
            default:
                throw new NotSupportedException(kind.ToString());
        }
    }

    static void writeCommandValues<TCommandValue>(TextWriter writer,
        ExportSettings exportSettings,
        TCommandValue startValue,
        TCommandValue endValue,
        bool exportEndValue = true)
        where TCommandValue : struct, ICommandValue<TCommandValue>
    {
        using var startValueText = startValue.ToOsbString(exportSettings);
        writer.Write(startValueText.AsReadOnlySpan());

        if (!exportEndValue) return;

        using var endValueText = endValue.ToOsbString(exportSettings);
        if (!startValueText.AsReadOnlySpan().SequenceEqual(endValueText.AsReadOnlySpan()))
        {
            writer.Write(',');
            writer.Write(endValueText.AsReadOnlySpan());
        }
    }
    
    internal static TTo commandValue<TFrom, TTo>(TFrom value)
        where TFrom : struct, ICommandValue<TFrom>
        where TTo : struct, ICommandValue<TTo>
    {
        if (typeof(TFrom) != typeof(TTo))
            throw new InvalidOperationException($"Command value type {typeof(TFrom).Name} cannot be used as {typeof(TTo).Name}.");

        return Unsafe.As<TFrom, TTo>(ref value);
    }
    
    internal readonly struct ViewEnumerable
    {
        readonly CommandChannel<TValue> channel;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ViewEnumerable(CommandChannel<TValue> channel) => this.channel = channel;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new(channel);
    }

    internal struct Enumerator
    {
        readonly CommandChannel<TValue> channel;
        int index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(CommandChannel<TValue> channel)
        {
            this.channel = channel;
            index = -1;
            Current = default;
        }

        public CommandView<TValue> Current { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            var next = index + 1;
            if (next >= channel.ViewCount) return false;

            index = next;
            Current = channel.ViewAt(next);
            return true;
        }
    }

}
