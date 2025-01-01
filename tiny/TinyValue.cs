namespace Tiny;

using System;
using System.Globalization;

public enum TinyTokenType
{
    Null, Boolean, Integer, Float, String, Object, Array, Invalid
}

public class TinyValue : TinyToken
{
    readonly TinyTokenType type;
    readonly object value;

    internal TinyValue(object value, TinyTokenType type)
    {
        this.value = value;
        this.type = type;

        switch (type)
        {
            case TinyTokenType.Object:
            case TinyTokenType.Array:
            case TinyTokenType.Invalid: throw new ArgumentOutOfRangeException(nameof(type), type.ToString());
        }
    }

    public TinyValue(object value) : this(value, FindValueType(value)) { }

    public TinyValue(string value) : this(value, TinyTokenType.String) { }

    public TinyValue(bool value) : this(value, TinyTokenType.Boolean) { }
    public TinyValue(bool? value) : this(value, TinyTokenType.Boolean) { }

    public TinyValue(sbyte value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(sbyte? value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(byte value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(byte? value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(short value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(short? value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(ushort value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(ushort? value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(int value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(int? value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(uint value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(uint? value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(long? value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(long value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(ulong value) : this(value, TinyTokenType.Integer) { }
    public TinyValue(ulong? value) : this(value, TinyTokenType.Integer) { }

    public TinyValue(float value) : this(value, TinyTokenType.Float) { }
    public TinyValue(float? value) : this(value, TinyTokenType.Float) { }
    public TinyValue(double value) : this(value, TinyTokenType.Float) { }
    public TinyValue(double? value) : this(value, TinyTokenType.Float) { }
    public TinyValue(decimal value) : this(value, TinyTokenType.Float) { }
    public TinyValue(decimal? value) : this(value, TinyTokenType.Float) { }

    public override bool IsInline => true;
    public override bool IsEmpty => value is null;
    public override TinyTokenType Type => type;

    public override T Value<T>(object key)
    {
        if (key is not null) throw new ArgumentException($"Key must be null, was {key}", nameof(key));

        if (value is T typedValue) return typedValue;

        var targetType = typeof(T);
        if (targetType == typeof(object)) return (T)value;

        if (targetType == typeof(TinyValue) || targetType == typeof(TinyToken)) return (T)(object)this;

        if (type is TinyTokenType.Null)
        {
            if (targetType == typeof(TinyArray)) return (T)(object)new TinyArray();
            if (targetType == typeof(TinyObject)) return (T)(object)new TinyObject();
        }

        if (targetType.IsEnum && type is TinyTokenType.String or TinyTokenType.Integer)
        {
            if (value is null) return default;

            return (T)Enum.Parse(targetType, value.ToString());
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (value is null) return default;

            targetType = Nullable.GetUnderlyingType(targetType);
        }

        return (T)Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    public static TinyTokenType FindValueType(object value) => value switch
    {
        null => TinyTokenType.Null,
        string => TinyTokenType.String,
        sbyte or byte or short or ushort or int or uint or long or ulong or Enum => TinyTokenType.Integer,
        float or double or decimal => TinyTokenType.Float,
        bool => TinyTokenType.Boolean,
        _ => TinyTokenType.Invalid
    };

    public override string ToString() => $"{value} ({type})";
}