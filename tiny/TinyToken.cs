namespace Tiny;

using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Formats;
using Formats.Json;
using Formats.Yaml;

public abstract class TinyToken
{
    public static readonly YamlFormat Yaml = new();
    public static readonly JsonFormat Json = new();
    public abstract bool IsInline { get; }
    public abstract bool IsEmpty { get; }
    public abstract TinyTokenType Type { get; }

    public static implicit operator TinyToken(string value) => new TinyValue(value);

    public static implicit operator TinyToken(bool value) => new TinyValue(value);
    public static implicit operator TinyToken(bool? value) => new TinyValue(value);

    public static implicit operator TinyToken(sbyte value) => new TinyValue(value);
    public static implicit operator TinyToken(sbyte? value) => new TinyValue(value);
    public static implicit operator TinyToken(byte value) => new TinyValue(value);
    public static implicit operator TinyToken(byte? value) => new TinyValue(value);
    public static implicit operator TinyToken(short value) => new TinyValue(value);
    public static implicit operator TinyToken(short? value) => new TinyValue(value);
    public static implicit operator TinyToken(ushort value) => new TinyValue(value);
    public static implicit operator TinyToken(ushort? value) => new TinyValue(value);
    public static implicit operator TinyToken(int value) => new TinyValue(value);
    public static implicit operator TinyToken(int? value) => new TinyValue(value);
    public static implicit operator TinyToken(uint value) => new TinyValue(value);
    public static implicit operator TinyToken(uint? value) => new TinyValue(value);
    public static implicit operator TinyToken(long? value) => new TinyValue(value);
    public static implicit operator TinyToken(long value) => new TinyValue(value);
    public static implicit operator TinyToken(ulong value) => new TinyValue(value);
    public static implicit operator TinyToken(ulong? value) => new TinyValue(value);

    public static implicit operator TinyToken(float value) => new TinyValue(value);
    public static implicit operator TinyToken(float? value) => new TinyValue(value);
    public static implicit operator TinyToken(double value) => new TinyValue(value);
    public static implicit operator TinyToken(double? value) => new TinyValue(value);
    public static implicit operator TinyToken(decimal value) => new TinyValue(value);
    public static implicit operator TinyToken(decimal? value) => new TinyValue(value);

    public abstract T Value<T>(object key);
    public T Value<T>() => Value<T>(null);

    public static TinyToken ToToken(object value)
    {
        if (value is TinyToken token) return token;

        if (TinyValue.FindValueType(value) != TinyTokenType.Invalid) return new TinyValue(value);
        switch (value)
        {
            case IDictionary dictionary:
            {
                TinyObject o = [];
                foreach (var key in dictionary.Keys) o.Add(Unsafe.As<string>(key), ToToken(dictionary[key]));
                return o;
            }
            case IEnumerable enumerable: return new TinyArray(enumerable);
            default: return new TinyValue(value);
        }
    }

    public static TinyToken Read(Stream stream, Format format)
    {
        using StreamReader reader = new(stream, Encoding.ASCII);
        return format.Read(reader);
    }

    public static TinyToken Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream, GetFormat(path));
    }

    public static TinyToken ReadString(string data, Format format)
    {
        using StringReader reader = new(data);
        return format.Read(reader);
    }

    public static TinyToken ReadString<T>(string data) where T : Format, new() => ReadString(data, new T());

    public void Write(Stream stream, Format format)
    {
        using StreamWriter writer = new(stream, Encoding.ASCII) { NewLine = "\n" };

        format.Write(writer, this);
    }

    public void Write(string path)
    {
        using var stream = File.Create(path);
        Write(stream, GetFormat(path));
    }

    public static Format GetFormat(string path)
    {
        var extension = Path.GetExtension(path);
        return extension switch
        {
            ".yml" or ".yaml" => Yaml,
            ".json" => Json,
            _ => throw new NotSupportedException($"No format matches extension '{extension}'.")
        };
    }
}