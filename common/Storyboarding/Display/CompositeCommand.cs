namespace StorybrewCommon.Storyboarding.Display;

using System;
using System.IO;
using Commands;
using CommandValues;

internal class CompositeCommand<TValue> : AnimatedValue<TValue>, ITypedCommand<TValue> where TValue : CommandValue
{
    public bool Active => true;
    public int CompareTo(ICommand other) => CommandComparer.CompareCommands(this, other);

    public void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation)
        => throw new InvalidOperationException();

    public override string ToString() => $"composite ({StartTime}s - {EndTime}s) : {StartValue} to {EndValue}";
}