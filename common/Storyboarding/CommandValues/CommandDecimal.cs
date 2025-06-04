namespace StorybrewCommon.Storyboarding.CommandValues;

using System;
using System.Runtime.InteropServices;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

///<summary> Custom decimal handler for storyboarding. </summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandDecimal : ICommandValue
{
    readonly double value;

#pragma warning disable CS1591
    CommandDecimal(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) this.value = 0;
        else this.value = value;
    }

    public bool Equals(CommandDecimal other) => value.Equals(other.value);

    public override int GetHashCode() => value.GetHashCode();

    public TempList<char> ToOsbString(ExportSettings exportSettings)
    {
        using var arr = ((float)value).ToCharArray(provider: exportSettings.NumberFormat);
        var span = arr.AsReadOnlySpan();

        return TempList<char>.Create(span[(span.StartsWith("0.") ? 1 : 0)..]);
    }

    public static CommandDecimal operator -(CommandDecimal left, CommandDecimal right) => left.value - right.value;
    public static CommandDecimal operator --(CommandDecimal value) => value.value - 1;
    public static CommandDecimal operator +(CommandDecimal left, CommandDecimal right) => left.value + right.value;
    public static CommandDecimal operator ++(CommandDecimal value) => value.value + 1;
    public static CommandDecimal operator *(CommandDecimal left, CommandDecimal right) => left.value * right.value;
    public static CommandDecimal operator /(CommandDecimal left, CommandDecimal right) => left.value / right.value;

    public static CommandDecimal operator -(CommandDecimal value) => -value.value;
    public static CommandDecimal operator +(CommandDecimal value) => value.value;

    public static implicit operator CommandDecimal(double value) => new(value);
    public static implicit operator double(CommandDecimal obj) => obj.value;
    public static implicit operator float(CommandDecimal obj) => (float)obj.value;
}