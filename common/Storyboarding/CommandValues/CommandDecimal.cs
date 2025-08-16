namespace StorybrewCommon.Storyboarding.CommandValues;

using System;
using System.Numerics;
using Tiny.PooledCollections.Generic.Temporary;

///<summary> Custom decimal handler for storyboarding. </summary>
public readonly record struct CommandDecimal : ICommandValue<CommandDecimal>, ISpanFormattable,
    IUnaryNegationOperators<CommandDecimal, CommandDecimal>
{
    const int FloatG7MaxChars = 16;
    readonly double value;

    CommandDecimal(double value) => this.value = value;

    TempList<char> ICommandValue<CommandDecimal>.ToOsbString(ExportSettings exportSettings)
    {
        Span<char> arr = stackalloc char[FloatG7MaxChars];
        value.TryFormat(arr, out var written, "G7", exportSettings.NumberFormat);

        var span = arr[..written];

        var result = TempList.Create<char>();
        if (span[0] == '-')
        {
            result.Add('-');
            span = span[1..];
        }

        if (span.Contains('.')) span = span.Trim('0');

        result.AddRange(span);
        return result;
    }

#pragma warning disable CS1591

    /// <inheritdoc/>
    public static CommandDecimal operator -(CommandDecimal left, CommandDecimal right) => new(left.value - right.value);

    public static CommandDecimal operator --(CommandDecimal value) => new(value.value - 1);

    /// <inheritdoc/>
    public static CommandDecimal operator +(CommandDecimal left, CommandDecimal right) => new(left.value + right.value);

    public static CommandDecimal operator ++(CommandDecimal value) => new(value.value + 1);
    public static CommandDecimal operator *(CommandDecimal left, CommandDecimal right) => new(left.value * right.value);

    public static CommandDecimal operator /(CommandDecimal left, CommandDecimal right) => new(left.value / right.value);

    /// <inheritdoc/>
    public static CommandDecimal operator -(CommandDecimal value) => new(-value.value);

    public static CommandDecimal operator +(CommandDecimal value) => new(double.Abs(value.value));

    public static implicit operator CommandDecimal(double value) => new(value);
    public static implicit operator double(CommandDecimal obj) => obj.value;
    public static implicit operator float(CommandDecimal obj) => (float)obj.value;

    string IFormattable.ToString(string format, IFormatProvider formatProvider)
        => value.ToString(string.IsNullOrWhiteSpace(format) ? "G7" : format, formatProvider);

    bool ISpanFormattable.TryFormat(Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider provider)
    {
        charsWritten = 0;

        Span<char> arr = stackalloc char[FloatG7MaxChars];
        if (!value.TryFormat(arr, out var written, format.IsEmpty ? "G7" : format, provider)) return false;

        var span = arr[..written];
        if (span[0] == '-')
        {
            if (destination.Length == 0) return false;

            destination[0] = '-';
            ++charsWritten;

            destination = destination[1..];
            span = span[1..];
        }

        if (span.Contains('.')) span = span.Trim('0');

        var result = span.TryCopyTo(destination);
        if (result) charsWritten += span.Length;
        return result;
    }
}