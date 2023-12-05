using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BrewLib.Util;

public static class HashHelper
{
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
        using var stream = File.OpenRead(path); return MD5.HashData(stream);
    }
}