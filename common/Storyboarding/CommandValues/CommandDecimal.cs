using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace StorybrewCommon.Storyboarding.CommandValues;

///<summary> Custom decimal handler for storyboarding. </summary>
public readonly struct CommandDecimal : CommandValue, ISignedNumber<CommandDecimal>, IEquatable<CommandDecimal>
{
    readonly double value;

    static CommandDecimal ISignedNumber<CommandDecimal>.NegativeOne => -1;
    static CommandDecimal INumberBase<CommandDecimal>.One => 1;
    static int INumberBase<CommandDecimal>.Radix => 2;
    static CommandDecimal INumberBase<CommandDecimal>.Zero => 0;
    static CommandDecimal IAdditiveIdentity<CommandDecimal, CommandDecimal>.AdditiveIdentity => 0;
    static CommandDecimal IMultiplicativeIdentity<CommandDecimal, CommandDecimal>.MultiplicativeIdentity => 1;

#pragma warning disable CS1591
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CommandDecimal(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) this.value = 0;
        else this.value = value;
    }

    public bool Equals(CommandDecimal other) => value.Equals(other.value);
    public override bool Equals(object obj) => obj is CommandDecimal deci && Equals(deci);

    public override int GetHashCode() => value.GetHashCode();
    public override string ToString() => ToOsbString(ExportSettings.Default);
    public string ToOsbString(ExportSettings exportSettings) => ((float)value).ToString(exportSettings.NumberFormat);

    static CommandDecimal INumberBase<CommandDecimal>.Abs(CommandDecimal value) => double.Abs(value.value);

    static bool INumberBase<CommandDecimal>.IsCanonical(CommandDecimal value) => true;
    static bool INumberBase<CommandDecimal>.IsComplexNumber(CommandDecimal value) => false;
    static bool INumberBase<CommandDecimal>.IsEvenInteger(CommandDecimal value) => double.IsEvenInteger(value.value);
    static bool INumberBase<CommandDecimal>.IsFinite(CommandDecimal value) => double.IsFinite(value.value);
    static bool INumberBase<CommandDecimal>.IsImaginaryNumber(CommandDecimal value) => false;
    static bool INumberBase<CommandDecimal>.IsInfinity(CommandDecimal value) => false;
    static bool INumberBase<CommandDecimal>.IsInteger(CommandDecimal value) => double.IsInteger(value.value);
    static bool INumberBase<CommandDecimal>.IsNaN(CommandDecimal value) => false;
    static bool INumberBase<CommandDecimal>.IsNegative(CommandDecimal value) => double.IsNegative(value.value);
    static bool INumberBase<CommandDecimal>.IsNegativeInfinity(CommandDecimal value) => false;
    static bool INumberBase<CommandDecimal>.IsNormal(CommandDecimal value) => double.IsNormal(value.value);
    static bool INumberBase<CommandDecimal>.IsOddInteger(CommandDecimal value) => double.IsOddInteger(value.value);
    static bool INumberBase<CommandDecimal>.IsPositive(CommandDecimal value) => double.IsPositive(value.value);
    static bool INumberBase<CommandDecimal>.IsPositiveInfinity(CommandDecimal value) => false;
    static bool INumberBase<CommandDecimal>.IsRealNumber(CommandDecimal value) => true;
    static bool INumberBase<CommandDecimal>.IsSubnormal(CommandDecimal value) => double.IsSubnormal(value.value);
    static bool INumberBase<CommandDecimal>.IsZero(CommandDecimal value) => value.value == 0;

    static CommandDecimal INumberBase<CommandDecimal>.MaxMagnitude(CommandDecimal x, CommandDecimal y) => double.MaxMagnitude(x, y);
    static CommandDecimal INumberBase<CommandDecimal>.MaxMagnitudeNumber(CommandDecimal x, CommandDecimal y) => double.MaxMagnitudeNumber(x, y);
    static CommandDecimal INumberBase<CommandDecimal>.MinMagnitude(CommandDecimal x, CommandDecimal y) => double.MinMagnitude(x, y);
    static CommandDecimal INumberBase<CommandDecimal>.MinMagnitudeNumber(CommandDecimal x, CommandDecimal y) => double.MinMagnitudeNumber(x, y);
    static CommandDecimal INumberBase<CommandDecimal>.Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider) => double.Parse(s, style, provider);
    static CommandDecimal INumberBase<CommandDecimal>.Parse(string s, NumberStyles style, IFormatProvider provider) => double.Parse(s.Trim(), style, provider);

    static bool INumberBase<CommandDecimal>.TryConvertFromChecked<TOther>(TOther value, out CommandDecimal result) => TryConvertFrom(value, out result);
    static bool INumberBase<CommandDecimal>.TryConvertFromSaturating<TOther>(TOther value, out CommandDecimal result) => TryConvertFrom(value, out result);
    static bool INumberBase<CommandDecimal>.TryConvertFromTruncating<TOther>(TOther value, out CommandDecimal result) => TryConvertFrom(value, out result);
    static bool INumberBase<CommandDecimal>.TryConvertToChecked<TOther>(CommandDecimal value, out TOther result)
    {
        try
        {
            result = TOther.CreateChecked(value);
            return true;
        }
        catch (SystemException)
        {
            result = default;
            return false;
        }
    }
    static bool INumberBase<CommandDecimal>.TryConvertToSaturating<TOther>(CommandDecimal value, out TOther result)
    {
        try
        {
            result = TOther.CreateSaturating(value);
            return true;
        }
        catch (SystemException)
        {
            result = default;
            return false;
        }
    }
    static bool INumberBase<CommandDecimal>.TryConvertToTruncating<TOther>(CommandDecimal value, out TOther result)
    {
        try
        {
            result = TOther.CreateTruncating(value);
            return true;
        }
        catch (SystemException)
        {
            result = default;
            return false;
        }
    }
    static bool TryConvertFrom<TOther>(TOther value, out CommandDecimal result) where TOther : INumberBase<TOther>
    {
        try
        {
            result = double.CreateChecked(value);
            return true;
        }
        catch (SystemException)
        {
            result = default;
            return false;
        }
    }

    static bool INumberBase<CommandDecimal>.TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out CommandDecimal result)
    {
        var success = double.TryParse(s, style, provider, out double dResult);
        result = dResult;
        return success;
    }
    static bool INumberBase<CommandDecimal>.TryParse(string s, NumberStyles style, IFormatProvider provider, out CommandDecimal result)
    {
        var success = double.TryParse(s, style, provider, out double dResult);
        result = dResult;
        return success;
    }
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider provider)
        => value.TryFormat(destination, out charsWritten, format, provider);

    string IFormattable.ToString(string format, IFormatProvider formatProvider) => value.ToString(format, formatProvider);
    static CommandDecimal ISpanParsable<CommandDecimal>.Parse(ReadOnlySpan<char> s, IFormatProvider provider) => double.Parse(s, provider);

    static bool ISpanParsable<CommandDecimal>.TryParse(ReadOnlySpan<char> s, IFormatProvider provider, out CommandDecimal result)
    {
        var success = double.TryParse(s, provider, out double dResult);
        result = dResult;
        return success;
    }

    static CommandDecimal IParsable<CommandDecimal>.Parse(string s, IFormatProvider provider) => double.Parse(s, provider);
    static bool IParsable<CommandDecimal>.TryParse(string s, IFormatProvider provider, out CommandDecimal result)
    {
        var success = double.TryParse(s, provider, out double dResult);
        result = dResult;
        return success;
    }

    public static CommandDecimal operator -(CommandDecimal left, CommandDecimal right) => left.value - right.value;
    public static CommandDecimal operator --(CommandDecimal value) => value.value - 1;
    public static CommandDecimal operator +(CommandDecimal left, CommandDecimal right) => left.value + right.value;
    public static CommandDecimal operator ++(CommandDecimal value) => value.value + 1;
    public static CommandDecimal operator *(CommandDecimal left, CommandDecimal right) => left.value * right.value;
    public static CommandDecimal operator /(CommandDecimal left, CommandDecimal right) => left.value / right.value;

    public static CommandDecimal operator -(CommandDecimal value) => -value.value;
    public static CommandDecimal operator +(CommandDecimal value) => value.value;

    public static bool operator ==(CommandDecimal left, CommandDecimal right) => left.value.Equals(right.value);
    public static bool operator !=(CommandDecimal left, CommandDecimal right) => !left.value.Equals(right.value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator CommandDecimal(double value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(CommandDecimal obj) => obj.value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(CommandDecimal obj) => unchecked((float)obj.value);
}