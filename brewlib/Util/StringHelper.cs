namespace BrewLib.Util;

using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Memory;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.Temporary;

public static class StringHelper
{
    static readonly string[] sizeOrders = ["b", "kb", "mb", "gb", "tb"];
    static readonly string utf8Bom = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

    public static readonly Pool<StringBuilder> StringBuilderPool = new(obj => obj.Length = 0);

    public static string ToByteSize(float byteCount, string format = "{0:0.##} {1}")
    {
        var order = 0;
        while (byteCount >= 1024 && order < sizeOrders.Length - 1)
        {
            ++order;
            byteCount /= 1024;
        }

        return string.Format(CultureInfo.InvariantCulture, format, byteCount, sizeOrders[order]);
    }

    public static string StripUtf8Bom(this string s)
        => s.StartsWith(utf8Bom, StringComparison.Ordinal) ? s.Remove(0, utf8Bom.Length) : s;

    public static string GetMd5(string value) => GetMd5(Encoding.ASCII.GetBytes(value));

    public static string GetMd5(byte[] data)
    {
        data = MD5.HashData(data);

        var chars = StringBuilderPool.Retrieve();
        foreach (var t in data) chars.Append(t.ToString("x2", CultureInfo.InvariantCulture));

        var str = chars.ToString();
        StringBuilderPool.Release(chars);
        return str;
    }

    public static string GetFileMd5(string path)
    {
        var data = GetFileMd5Bytes(path);

        var chars = StringBuilderPool.Retrieve();
        foreach (var t in data) chars.Append(t.ToString("x2", CultureInfo.InvariantCulture));

        var str = chars.ToString();
        StringBuilderPool.Release(chars);
        return str;
    }

    public static byte[] GetFileMd5Bytes(string path)
    {
        using var stream = File.OpenRead(path);
        return MD5.HashData(stream);
    }

    public static StringBuilder TrimEnd(this StringBuilder sb)
    {
        var i = sb.Length - 1;
        for (; i >= 0; --i)
            if (!char.IsWhiteSpace(sb[i]))
                break;

        if (i < sb.Length - 1) sb.Length = i + 1;
        return sb;
    }

    public static TempArray<char> ToCharArray<T>(this T value,
        ReadOnlySpan<char> format = default,
        IFormatProvider provider = null) where T : ISpanFormattable
    {
        Span<char> temp = stackalloc char[128];
        value.TryFormat(temp, out var written, format, provider);

        return TempArray<char>.Create(temp[..written]);
    }

    public static void AddRangeFormatted<T>(this scoped ref TempList<char> list,
        T value,
        ReadOnlySpan<char> format = default,
        IFormatProvider provider = null) where T : ISpanFormattable
    {
        Span<char> temp = stackalloc char[128];
        value.TryFormat(temp, out var written, format, provider);

        list.AddRange(temp[..written]);
    }

    public static void AddRangeEnum<T>(this scoped ref TempList<char> list, T value, ReadOnlySpan<char> format = default)
        where T : struct, Enum
    {
        Span<char> temp = stackalloc char[128];
        Enum.TryFormat(value, temp, out var written, format);

        list.AddRange(temp[..written]);
    }

    public static int GetDigitCount<T>(T value) where T : IBinaryInteger<T>
    {
        var result = 1;
        switch (value)
        {
            case 0: return 1;

            case < 0:
            {
                value = T.Abs(value);
                ++result;

                break;
            }
        }

        return (int)float.Log10(float.CreateChecked(value)) + result;
    }

    public static TempList<ValueList<char>> Split(this scoped ReadOnlySpan<char> value, ReadOnlySpan<char> separator)
    {
        var enumerator = MemoryExtensions.Split(value, separator);
        var list = TempList<ValueList<char>>.Create();

        foreach (var s in enumerator) list.Add(ValueList<char>.Create(value[s]));

        return list;
    }
}