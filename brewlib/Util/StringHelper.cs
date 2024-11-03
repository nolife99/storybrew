using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BrewLib.Util;

public static class StringHelper
{
    static readonly string[] sizeOrders = ["b", "kb", "mb", "gb", "tb"];

    public static string ToByteSize(float byteCount, string format = "{0:0.##} {1}")
    {
        var order = 0;
        while (byteCount >= 1024 && order < sizeOrders.Length - 1)
        {
            ++order;
            byteCount /= 1024;
        }
        return string.Format(CultureInfo.CurrentCulture, format, byteCount, sizeOrders[order]);
    }

    static readonly string utf8Bom = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
    public static string StripUtf8Bom(this string s) => s.StartsWith(utf8Bom, System.StringComparison.Ordinal) ? s.Remove(0, utf8Bom.Length) : s;

    public static string GetMd5(string value) => GetMd5(Encoding.ASCII.GetBytes(value));
    public static string GetMd5(byte[] data)
    {
        data = MD5.HashData(data);

        StringBuilder chars = new(data.Length * 2);
        for (var i = 0; i < data.Length; ++i) chars.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));

        return chars.ToString();
    }

    public static string GetFileMd5(string path)
    {
        var data = GetFileMd5Bytes(path);

        StringBuilder chars = new(data.Length * 2);
        for (var i = 0; i < data.Length; ++i) chars.Append(data[i].ToString("x2", CultureInfo.InvariantCulture));

        return chars.ToString();
    }
    public static byte[] GetFileMd5Bytes(string path)
    {
        using var stream = File.OpenRead(path);
        return MD5.HashData(stream);
    }
}