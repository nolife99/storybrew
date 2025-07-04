namespace BrewLib.Util;

using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public static class StringHelper
{
    static readonly string[] sizeOrders = ["b", "kb", "mb", "gb", "tb"];
    static readonly string utf8Bom = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

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

    public static string GetMd5(scoped ReadOnlySpan<char> value) => GetMd5(Encoding.ASCII.GetBytes(value.ToString()));

    public static string GetMd5(byte[] data)
    {
        data = MD5.HashData(data);

        using var chars = TempList.Create<char>();
        foreach (var t in data) chars.AppendFormatted(t, "x2", CultureInfo.InvariantCulture);

        return chars.AsReadOnlySpan().ToString();
    }

    public static string GetFileMd5(string path)
    {
        var data = GetFileMd5Bytes(path);

        using var chars = TempList.Create<char>();
        foreach (var t in data) chars.AppendFormatted(t, "x2", CultureInfo.InvariantCulture);

        return chars.AsReadOnlySpan().ToString();
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
        scoped ReadOnlySpan<char> format = default,
        IFormatProvider provider = null) where T : ISpanFormattable
    {
        Span<char> temp = stackalloc char[128];
        value.TryFormat(temp, out var written, format, provider);

        return TempArray.Create<char>(temp[..written]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Append(this scoped ref readonly TempList<char> list, string value)
        => Unsafe.AsRef(in list).AddRange(value.AsSpan());

    public static void AppendFormatted<T>(this scoped ref readonly TempList<char> list,
        T value,
        scoped ReadOnlySpan<char> format = default,
        IFormatProvider provider = null) where T : ISpanFormattable
    {
        Span<char> temp = stackalloc char[128];
        value.TryFormat(temp, out var written, format, provider);

        Unsafe.AsRef(in list).AddRange(temp[..written]);
    }

    public static void AppendEnum<T>(this scoped ref readonly TempList<char> list,
        T value,
        scoped ReadOnlySpan<char> format = default) where T : struct, Enum
    {
        Span<char> temp = stackalloc char[128];
        Enum.TryFormat(value, temp, out var written, format);

        Unsafe.AsRef(in list).AddRange(temp[..written]);
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

    public static TempList<ValueList<char>> Split(this scoped ReadOnlySpan<char> value, scoped ReadOnlySpan<char> separator)
    {
        var enumerator = MemoryExtensions.Split(value, separator);
        var list = TempList.Create<ValueList<char>>();

        foreach (var s in enumerator) list.Add(ValueList.Create(value[s]));

        return list;
    }
}