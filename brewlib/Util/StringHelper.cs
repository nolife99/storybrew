namespace BrewLib.Util;

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.ObjectPool;

public static class StringHelper
{
    static readonly string[] sizeOrders = ["b", "kb", "mb", "gb", "tb"];
    static readonly string utf8Bom = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

    public static readonly ObjectPool<StringBuilder> StringBuilderPool = ObjectPool.Create(new StringBuilderPooledObjectPolicy());

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

        var chars = StringBuilderPool.Get();
        foreach (var t in data) chars.Append(t.ToString("x2", CultureInfo.InvariantCulture));

        var str = chars.ToString();
        StringBuilderPool.Return(chars);
        return str;
    }
    public static string GetFileMd5(string path)
    {
        var data = GetFileMd5Bytes(path);

        var chars = StringBuilderPool.Get();
        foreach (var t in data) chars.Append(t.ToString("x2", CultureInfo.InvariantCulture));

        var str = chars.ToString();
        StringBuilderPool.Return(chars);
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
}