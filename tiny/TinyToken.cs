﻿using System;
using System.Collections;
using System.IO;
using System.Text;
using Tiny.Formats;
using Tiny.Formats.Json;
using Tiny.Formats.Yaml;

namespace Tiny;

public abstract class TinyToken
{
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

        if (TinyValue.FindValueType(value) == TinyTokenType.Invalid)
        {
            if (value is IDictionary dictionary)
            {
                TinyObject o = [];
                foreach (var key in dictionary.Keys) o.Add((string)key, ToToken(dictionary[key]));
                return o;
            }

            if (value is IEnumerable enumerable) return new TinyArray(enumerable);
        }

        return new TinyValue(value);
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
        using StreamWriter writer = new(stream, Encoding.ASCII)
        {
            NewLine = "\n"
        };
        format.Write(writer, this);
    }
    public void Write(string path)
    {
        using var stream = File.Create(path);
        Write(stream, GetFormat(path));
    }

    public readonly static YamlFormat Yaml = new();
    public readonly static JsonFormat Json = new();

    public static Format GetFormat(string path)
    {
        var extension = Path.GetExtension(path);
        return extension switch
        {
            ".yml" or ".yaml" => Yaml,
            ".json" => Json,
            _ => throw new NotImplementedException($"No format matches extension '{extension}'."),
        };
    }
}