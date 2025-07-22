namespace StorybrewCommon.Storyboarding.CommandValues;

using System;
using System.Runtime.InteropServices;
using Tiny.PooledCollections.Generic.Temporary;

///<summary> Custom decimal handler for storyboarding. </summary>
[StructLayout(LayoutKind.Sequential)] public readonly record struct CommandDecimal : ICommandValue
{
    readonly double value;

#pragma warning disable CS1591
    CommandDecimal(double value)
    {
        if (!double.IsFinite(this.value)) this.value = 0;
        else this.value = value;
    }

    public bool Equals(CommandDecimal other) => value.Equals(other.value);

    public override int GetHashCode() => value.GetHashCode();

    public TempList<char> ToOsbString(ExportSettings exportSettings)
    {
        Span<char> arr = stackalloc char[128];
        double.Round(value, 6).TryFormat(arr, out var written, provider: exportSettings.NumberFormat);

        var span = arr[..written];

        var result = TempList.Create<char>();
        if (span[0] == '-')
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
    public static CommandDecimal operator +(CommandDecimal value) => new(double.Abs(value.value));

    public static implicit operator CommandDecimal(double value) => new(value);
    public static implicit operator double(CommandDecimal obj) => obj.value;
    public static implicit operator float(CommandDecimal obj) => (float)obj.value;
}