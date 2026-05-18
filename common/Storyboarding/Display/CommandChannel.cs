namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Animations;
using StorybrewCommon.Storyboarding.Commands;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Temporary.Internals;

class CommandChannel<TValue> where TValue : struct, ICommandValue<TValue>
{
    const ushort EasingMask = 0x007F;
    const ushort MaintainValueMask = 0x0080;
    const ushort KindMask = 0x0F00;
    const int KindShift = 8;

    protected readonly List<float> startTimes = [];
    protected readonly List<CommandData> commands = [];

    public bool HasOverlap;
    public int Count => startTimes.Count;

    protected record struct CommandData(float EndTime, TValue StartValue, TValue EndValue, ushort Flags);

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
        var commandSpan = CollectionsMarshal.AsSpan(commands);

        HasOverlap |= index > 0 && startTime < commandSpan[index - 1].EndTime ||
                      index < Count && startTimes[index] < endTime;

        startTimes.Insert(index, startTime);
        commands.Insert(index, new(endTime, startValue, endValue, packFlags(kind, easing, maintainValue)));

        return true;
    }

    int findInsertIndex(float startTime, float endTime)
    {
        var lo = 0;
        var hi = Count;
        var commandSpan = CollectionsMarshal.AsSpan(commands);

        while (lo < hi)
        {
            var mid = lo + hi >>> 1;
            var midStart = startTimes[mid];

            if (startTime < midStart || startTime == midStart && endTime < commandSpan[mid].EndTime) hi = mid;
            else lo = mid + 1;
        }

        return lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ushort packFlags(CommandKind kind, OsbEasing easing, bool maintainValue)
    {
        return (ushort)((byte)easing |
                        (maintainValue ? MaintainValueMask : 0) |
                        (byte)kind << KindShift);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static OsbEasing easingFromFlags(ushort flags) => (OsbEasing)(flags & EasingMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool maintainsValueFromFlags(ushort flags) => (flags & MaintainValueMask) != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static CommandKind kindFromFlags(ushort flags) => (CommandKind)((flags & KindMask) >> KindShift);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float StartTimeAt(int index) => startTimes[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float EndTimeAt(int index) => commands[index].EndTime;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue StartValueAt(int index) => commands[index].StartValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue EndValueAt(int index) => commands[index].EndValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OsbEasing EasingAt(int index) => easingFromFlags(commands[index].Flags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CommandKind KindAt(int index) => kindFromFlags(commands[index].Flags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MaintainsValueAt(int index) => maintainsValueFromFlags(commands[index].Flags);

    protected int FindIndex(float time)
    {
        var index = findIndexCore(time);
        if (index <= 0) return index;

        ref readonly var previous = ref CollectionsMarshal.AsSpan(commands)[index - 1];
        return float.ConvertToIntegerNative<int>(float.Round(time)) == float.ConvertToIntegerNative<int>(float.Round(previous.EndTime)) ? index - 1 : index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int findIndexCore(float time)
    {
        var count = Count;
        if (count == 0) return -1;

        var times = CollectionsMarshal.AsSpan(startTimes);
        ref var first = ref MemoryMarshal.GetReference(times);

        if (time < first) return 0;

        var last = count - 1;
        if (time > Unsafe.Add(ref first, last)) return last;

        var lower = lowerBound(ref first, count, time);
        return Unsafe.Add(ref first, lower) == time ? lower : lower - 1;
    }

    protected int FindIndexSlowOverlap(float time)
    {
        var index = findIndexCore(time);
        if (index <= 0) return index;

        var times = CollectionsMarshal.AsSpan(startTimes);
        var commandSpan = CollectionsMarshal.AsSpan(commands);
        var indexStart = times[index];

        for (var i = 0; i < index; i++)
            if (times[i] <= indexStart && time <= commandSpan[i].EndTime)
                return i;

        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int lowerBound(ref float first, int count, float time)
    {
        if (count < Vector128<float>.Count)
            return lowerBoundScalar(ref first, count, time);

        if (Vector512.IsHardwareAccelerated && count >= Vector512<float>.Count)
            return lowerBoundVector512(ref first, count, time);

        if (Vector256.IsHardwareAccelerated && count >= Vector256<float>.Count)
            return lowerBoundVector256(ref first, count, time);

        if (Vector128.IsHardwareAccelerated)
            return lowerBoundVector128(ref first, count, time);

        return lowerBoundBinary(ref first, count, time);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int lowerBoundScalar(ref float first, int count, float time)
    {
        for (var i = 0; i < count; i++)
            if (time <= Unsafe.Add(ref first, i))
                return i;

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int lowerBoundBinary(ref float first, int count, float time)
    {
        var lo = 0;
        var hi = count;

        while (lo < hi)
        {
            var mid = lo + hi >>> 1;

            if (time <= Unsafe.Add(ref first, mid))
                hi = mid;
            else
                lo = mid + 1;
        }

        return lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int lowerBoundVector128(ref float first, int count, float time)
    {
        var lo = 0;
        var hi = count;

        while (hi - lo > Vector128<float>.Count)
        {
            var mid = lo + hi >>> 1;

            if (time <= Unsafe.Add(ref first, mid))
                hi = mid;
            else
                lo = mid + 1;
        }

        var blockStart = hi >= Vector128<float>.Count ? hi - Vector128<float>.Count : 0;

        var values = Vector128.LoadUnsafe(in first, (nuint)blockStart);
        var needle = Vector128.Create(time);

        var mask = Vector128.LessThan(values, needle).ExtractMostSignificantBits();

        return blockStart + BitOperations.PopCount(mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int lowerBoundVector256(ref float first, int count, float time)
    {
        var lo = 0;
        var hi = count;

        while (hi - lo > Vector256<float>.Count)
        {
            var mid = lo + hi >>> 1;

            if (time <= Unsafe.Add(ref first, mid))
                hi = mid;
            else
                lo = mid + 1;
        }

        var blockStart = hi >= Vector256<float>.Count ? hi - Vector256<float>.Count : 0;

        var values = Vector256.LoadUnsafe(in first, (nuint)blockStart);
        var needle = Vector256.Create(time);

        var mask = Vector256.LessThan(values, needle).ExtractMostSignificantBits();

        return blockStart + BitOperations.PopCount(mask);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int lowerBoundVector512(ref float first, int count, float time)
    {
        var lo = 0;
        var hi = count;

        while (hi - lo > Vector512<float>.Count)
        {
            var mid = lo + hi >>> 1;

            if (time <= Unsafe.Add(ref first, mid))
                hi = mid;
            else
                lo = mid + 1;
        }

        var blockStart = hi >= Vector512<float>.Count ? hi - Vector512<float>.Count : 0;

        var values = Vector512.LoadUnsafe(in first, (nuint)blockStart);
        var needle = Vector512.Create(time);

        var mask = Vector512.LessThan(values, needle).ExtractMostSignificantBits();

        return blockStart + BitOperations.PopCount(mask);
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
        ref readonly var command = ref CollectionsMarshal.AsSpan(commands)[index];

        if (time < startTime)
            return maintainsValueFromFlags(command.Flags) ? command.StartValue : defaultValue;

        var endTime = command.EndTime;
        if (endTime < time)
            return maintainsValueFromFlags(command.Flags) ? command.EndValue : defaultValue;

        var duration = endTime - startTime;
        return command.StartValue + (command.EndValue - command.StartValue) *
            (duration > 0 ? easingFromFlags(command.Flags).Ease((time - startTime) / duration) : 0);
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
    {
        ref readonly var command = ref CollectionsMarshal.AsSpan(commands)[index];

        return new(kindFromFlags(command.Flags),
            startTimes[index],
            command.EndTime,
            timeOffset,
            easingFromFlags(command.Flags),
            command.StartValue,
            command.EndValue);
    }

    internal void OffsetAll(float offset)
    {
        var commandSpan = CollectionsMarshal.AsSpan(commands);

        for (var i = 0; i < Count; i++)
        {
            startTimes[i] += offset;
            commandSpan[i].EndTime += offset;
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
            var commandSpan = CollectionsMarshal.AsSpan(commands);

            for (var i = 0; i < Count; i++) result = float.Max(result, commandSpan[i].EndTime);
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

        ref readonly var command = ref CollectionsMarshal.AsSpan(commands)[index];
        var kind = kindFromFlags(command.Flags);

        writeIdentifier(writer, kind);
        writer.Write(',');
        writeInt(writer, (int)easingFromFlags(command.Flags));
        writer.Write(',');

        var startTime = startTimes[index];
        var endTime = command.EndTime;
        writeTime(writer, exportSettings, startTime);
        writer.Write(',');

        if (!sameExportTime(exportSettings, startTime, endTime))
            writeTime(writer, exportSettings, endTime);

        writer.Write(',');
        writeValues(writer, exportSettings, in transform, in command, kind);
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
        scoped ref readonly CommandData command,
        CommandKind kind)
    {
        switch (kind)
        {
            case CommandKind.Move:
            {
                var start = commandValue<TValue, CommandPosition>(command.StartValue);
                var end = commandValue<TValue, CommandPosition>(command.EndValue);
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
                var start = commandValue<TValue, CommandDecimal>(command.StartValue);
                var end = commandValue<TValue, CommandDecimal>(command.EndValue);
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
                var start = commandValue<TValue, CommandDecimal>(command.StartValue);
                var end = commandValue<TValue, CommandDecimal>(command.EndValue);
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
                var start = commandValue<TValue, CommandDecimal>(command.StartValue);
                var end = commandValue<TValue, CommandDecimal>(command.EndValue);
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
                var start = commandValue<TValue, CommandScale>(command.StartValue);
                var end = commandValue<TValue, CommandScale>(command.EndValue);
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
                var start = commandValue<TValue, CommandDecimal>(command.StartValue);
                var end = commandValue<TValue, CommandDecimal>(command.EndValue);
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
                    commandValue<TValue, CommandDecimal>(command.StartValue),
                    commandValue<TValue, CommandDecimal>(command.EndValue));
                break;
            case CommandKind.Color:
                writeCommandValues(writer,
                    exportSettings,
                    commandValue<TValue, CommandColor>(command.StartValue),
                    commandValue<TValue, CommandColor>(command.EndValue));
                break;
            case CommandKind.Additive:
            case CommandKind.FlipH:
            case CommandKind.FlipV:
                writeCommandValues(writer,
                    exportSettings,
                    commandValue<TValue, CommandParameter>(command.StartValue),
                    commandValue<TValue, CommandParameter>(command.StartValue),
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
        if (startValueText.AsReadOnlySpan().SequenceEqual(endValueText.AsReadOnlySpan())) return;

        writer.Write(',');
        writer.Write(endValueText.AsReadOnlySpan());
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