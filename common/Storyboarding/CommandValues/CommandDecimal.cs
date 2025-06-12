namespace StorybrewCommon.Storyboarding.CommandValues;

using System;
using System.Runtime.InteropServices;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

///<summary> Custom decimal handler for storyboarding. </summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandDecimal : ICommandValue
{
    readonly decimal value;

#pragma warning disable CS1591
    CommandDecimal(decimal value) => this.value = value;

    public bool Equals(CommandDecimal other) => value.Equals(other.value);

    public override int GetHashCode() => value.GetHashCode();

    public TempList<char> ToOsbString(ExportSettings exportSettings)
    {
        using var arr = decimal.Round(value, 6).ToCharArray(provider: exportSettings.NumberFormat);
        var span = arr.AsReadOnlySpan();

        var result = TempList<char>.Create();
        if (span.StartsWith('-'))
        {
            result.Add('-');
            span = span.TrimStart('-');
        }

        if (span.Contains('.')) span = span.Trim('0');

        result.AddRange(span);
        return result;
    }

    public static CommandDecimal operator -(CommandDecimal left, CommandDecimal right) => new(left.value - right.value);
    public static CommandDecimal operator --(CommandDecimal value) => new(value.value - 1);
    public static CommandDecimal operator +(CommandDecimal left, CommandDecimal right) => new(left.value + right.value);
    public static CommandDecimal operator ++(CommandDecimal value) => new(value.value + 1);
    public static CommandDecimal operator *(CommandDecimal left, CommandDecimal right) => new(left.value * right.value);
    public static CommandDecimal operator /(CommandDecimal left, CommandDecimal right) => new(left.value / right.value);

    public static CommandDecimal operator -(CommandDecimal value) => new(-value.value);
    public static CommandDecimal operator +(CommandDecimal value) => new(decimal.Abs(value.value));

    public static implicit operator CommandDecimal(double value) => new((decimal)value);
    public static implicit operator double(CommandDecimal obj) => (double)obj.value;
    public static implicit operator float(CommandDecimal obj) => (float)obj.value;
}