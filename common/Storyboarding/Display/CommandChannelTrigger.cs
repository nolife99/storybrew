namespace StorybrewCommon.Storyboarding.Display;

using System.IO;
using BrewLib.Util;
using StorybrewCommon.Storyboarding.CommandValues;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

sealed class CommandChannelTrigger<TValue> : CommandChannel<TValue> where TValue : struct, ICommandValue<TValue>
{
    public int Id;
    public bool Active;
    public float TriggerTime;

    public string TriggerName;
    public float TriggerStartTime, TriggerEndTime;
    public int Group;

    internal TValue ValueAtTimeDirect(float time, TValue defaultValue)
        => !Active ? defaultValue : base.ValueAtTimeDirect(time - TriggerTime, defaultValue);

    public override bool ResultAtTime(float time, out CommandResult<TValue> result)
    {
        if (!Active)
        {
            result = default;
            return false;
        }

        if (!base.ResultAtTime(time - TriggerTime, out result)) return false;
        result = new(result.Channel!, result.Index, TriggerTime);
        return true;
    }

    internal override void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        scoped ref readonly StoryboardTransform transform,
        int indentation)
    {
        if (Count == 0) return;

        writeIndent(writer, indentation);
        writer.Write("T,");
        writer.Write(TriggerName);
        writer.Write(',');
        writeTime(writer, exportSettings, TriggerStartTime);
        writer.Write(',');
        writeTime(writer, exportSettings, TriggerEndTime);
        writer.Write(',');
        writeInt(writer, Group);
        writer.WriteLine();

        for (var i = 0; i < Count; i++) writeCommand(writer, exportSettings, in transform, indentation + 1, i);
    }
}
