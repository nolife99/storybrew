namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.IO;
using BrewLib.Util;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

sealed class CommandChannelLoop<TValue> : CommandChannel<TValue> where TValue : struct, ICommandValue<TValue>
{
    public int Id;
    public int LoopCount = 1;
    public float LoopStartTime, LoopDuration;

    internal override int ViewCount
    {
        get
        {
            var count = Count;
            return count == 0 ? 0 : count * LoopCount;
        }
    }

    internal override CommandView<TValue> ViewAt(int viewIndex)
    {
        var count = Count;
        var loopIndex = viewIndex / count;
        var commandIndex = viewIndex - loopIndex * count;
        return GetView(commandIndex, LoopStartTime + loopIndex * LoopDuration);
    }

    internal TValue ValueAtTimeDirect(float time, TValue defaultValue)
    {
        if (Count == 0) return defaultValue;

        var startTime = LoopStartTime;
        if (time < startTime) return ValueAtIndex(0, startTimes[0], defaultValue);

        var loopTime = time - startTime;
        var loopDuration = LoopDuration;
        if (loopDuration <= 0) return ValueAtIndex(Count - 1, endTimes[^1], defaultValue);

        if (loopTime >= LoopCount * loopDuration)
            return ValueAtIndex(Count - 1, time - (startTime + (LoopCount - 1) * loopDuration), defaultValue);

        if (loopTime < loopDuration) return ValueAtIndex(HasOverlap ? FindIndexSlowOverlap(loopTime) : FindIndex(loopTime), loopTime, defaultValue);

        var loopNumber = float.ConvertToIntegerNative<int>(loopTime / loopDuration);
        loopTime %= loopDuration;

        if (loopTime <= startTimes[0])
            return ValueAtIndex(Count - 1, loopTime + loopDuration, defaultValue);

        return ValueAtIndex(HasOverlap ? FindIndexSlowOverlap(loopTime) : FindIndex(loopTime), loopTime, defaultValue);
    }

    public override bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        if (Count == 0)
        {
            result = default;
            return false;
        }

        var startTime = LoopStartTime;
        if (time < startTime)
        {
            result = new(this, 0, startTime);
            return true;
        }

        var loopTime = time - startTime;
        var loopDuration = LoopDuration;
        if (loopDuration <= 0)
        {
            result = new(this, Count - 1, startTime);
            return true;
        }

        if (loopTime >= LoopCount * loopDuration)
        {
            result = new(this, Count - 1, startTime + (LoopCount - 1) * loopDuration);
            return true;
        }

        if (loopTime < loopDuration)
        {
            result = new(this, HasOverlap ? FindIndexSlowOverlap(loopTime) : FindIndex(loopTime), startTime);
            return true;
        }

        var loopNumber = float.ConvertToIntegerNative<int>(loopTime / loopDuration);
        loopTime %= loopDuration;

        if (loopTime <= startTimes[0])
        {
            result = new(this, Count - 1, startTime + (loopNumber - 1) * loopDuration);
            return true;
        }

        result = new(this,
            HasOverlap ? FindIndexSlowOverlap(loopTime) : FindIndex(loopTime),
            startTime + loopNumber * loopDuration);
        return true;
    }

    internal override void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        scoped ref readonly StoryboardTransform transform,
        int indentation)
    {
        if (Count == 0) return;

        writeIndent(writer, indentation);
        writer.Write("L,");
        writeTime(writer, exportSettings, LoopStartTime);
        writer.Write(',');
        writeInt(writer, LoopCount);
        writer.WriteLine();

        for (var i = 0; i < Count; i++) writeCommand(writer, exportSettings, in transform, indentation + 1, i);
    }
}
