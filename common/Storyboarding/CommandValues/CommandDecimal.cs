using System;
using System.Globalization;
using System.Numerics;

namespace StorybrewCommon.Storyboarding.CommandValues
{
    ///<summary> Custom decimal handler for storyboarding. </summary>
    [Serializable] public readonly struct CommandDecimal : CommandValue, ISignedNumber<CommandDecimal>, IEquatable<CommandDecimal>
    {
        readonly double value;

        static CommandDecimal ISignedNumber<CommandDecimal>.NegativeOne => -1;
        static CommandDecimal INumberBase<CommandDecimal>.One => 1;
        static int INumberBase<CommandDecimal>.Radix => 2;
        static CommandDecimal INumberBase<CommandDecimal>.Zero => 0;
        static CommandDecimal IAdditiveIdentity<CommandDecimal, CommandDecimal>.AdditiveIdentity => 0;
        static CommandDecimal IMultiplicativeIdentity<CommandDecimal, CommandDecimal>.MultiplicativeIdentity => 1;

#pragma warning disable CS1591
        public CommandDecimal(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                this.value = 0;
                return;
            }
            this.value = value;
        }

        public bool Equals(CommandDecimal other) => value.Equals(other.value);
        public override bool Equals(object obj) => obj is CommandDecimal deci && Equals(deci);

        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => ToOsbString(ExportSettings.Default);
        public float DistanceFrom(object obj) => (float)Math.Abs(value - ((CommandDecimal)obj).value);
        public string ToOsbString(ExportSettings exportSettings) => ((float)value).ToString(exportSettings.NumberFormat);

        static CommandDecimal INumberBase<CommandDecimal>.Abs(CommandDecimal value) => Math.Abs(value.value);

        static bool INumberBase<CommandDecimal>.IsCanonical(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsComplexNumber(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsEvenInteger(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsFinite(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsImaginaryNumber(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsInfinity(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsInteger(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsNaN(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsNegative(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsNegativeInfinity(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsNormal(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsOddInteger(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsPositive(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsPositiveInfinity(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsRealNumber(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsSubnormal(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.IsZero(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal INumberBase<CommandDecimal>.MaxMagnitude(CommandDecimal x, CommandDecimal y)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal INumberBase<CommandDecimal>.MaxMagnitudeNumber(CommandDecimal x, CommandDecimal y)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal INumberBase<CommandDecimal>.MinMagnitude(CommandDecimal x, CommandDecimal y)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal INumberBase<CommandDecimal>.MinMagnitudeNumber(CommandDecimal x, CommandDecimal y)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal INumberBase<CommandDecimal>.Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal INumberBase<CommandDecimal>.Parse(string s, NumberStyles style, IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.TryConvertFromChecked<TOther>(TOther value, out CommandDecimal result)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.TryConvertFromSaturating<TOther>(TOther value, out CommandDecimal result)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.TryConvertFromTruncating<TOther>(TOther value, out CommandDecimal result)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.TryConvertToChecked<TOther>(CommandDecimal value, out TOther result)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.TryConvertToSaturating<TOther>(CommandDecimal value, out TOther result)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.TryConvertToTruncating<TOther>(CommandDecimal value, out TOther result)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out CommandDecimal result)
        {
            throw new NotImplementedException();
        }

        static bool INumberBase<CommandDecimal>.TryParse(string s, NumberStyles style, IFormatProvider provider, out CommandDecimal result)
        {
            throw new NotImplementedException();
        }

        bool IEquatable<CommandDecimal>.Equals(CommandDecimal other)
        {
            throw new NotImplementedException();
        }

        bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        string IFormattable.ToString(string format, IFormatProvider formatProvider)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal ISpanParsable<CommandDecimal>.Parse(ReadOnlySpan<char> s, IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        static bool ISpanParsable<CommandDecimal>.TryParse(ReadOnlySpan<char> s, IFormatProvider provider, out CommandDecimal result)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal IParsable<CommandDecimal>.Parse(string s, IFormatProvider provider)
        {
            throw new NotImplementedException();
        }

        static bool IParsable<CommandDecimal>.TryParse(string s, IFormatProvider provider, out CommandDecimal result)
        {
            throw new NotImplementedException();
        }

        public static CommandDecimal operator -(CommandDecimal left, CommandDecimal right) => left.value - right.value;
        public static CommandDecimal operator +(CommandDecimal left, CommandDecimal right) => left.value + right.value;
        public static bool operator ==(CommandDecimal left, CommandDecimal right) => left.Equals(right);
        public static bool operator !=(CommandDecimal left, CommandDecimal right) => !left.Equals(right);
        public static implicit operator CommandDecimal(double value) => new(value);
        public static implicit operator double(CommandDecimal obj) => obj.value;
        public static implicit operator float(CommandDecimal obj) => (float)obj.value;

        static CommandDecimal IAdditionOperators<CommandDecimal, CommandDecimal, CommandDecimal>.operator +(CommandDecimal left, CommandDecimal right)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal IDecrementOperators<CommandDecimal>.operator --(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal IDivisionOperators<CommandDecimal, CommandDecimal, CommandDecimal>.operator /(CommandDecimal left, CommandDecimal right)
        {
            throw new NotImplementedException();
        }

        static bool IEqualityOperators<CommandDecimal, CommandDecimal, bool>.operator ==(CommandDecimal left, CommandDecimal right)
        {
            throw new NotImplementedException();
        }

        static bool IEqualityOperators<CommandDecimal, CommandDecimal, bool>.operator !=(CommandDecimal left, CommandDecimal right)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal IIncrementOperators<CommandDecimal>.operator ++(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal IMultiplyOperators<CommandDecimal, CommandDecimal, CommandDecimal>.operator *(CommandDecimal left, CommandDecimal right)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal ISubtractionOperators<CommandDecimal, CommandDecimal, CommandDecimal>.operator -(CommandDecimal left, CommandDecimal right)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal IUnaryNegationOperators<CommandDecimal, CommandDecimal>.operator -(CommandDecimal value)
        {
            throw new NotImplementedException();
        }

        static CommandDecimal IUnaryPlusOperators<CommandDecimal, CommandDecimal>.operator +(CommandDecimal value)
        {
            throw new NotImplementedException();
        }
    }
}