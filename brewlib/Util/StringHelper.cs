using System.Globalization;
using System.Text;

namespace BrewLib.Util;

public static class StringHelper
{
    static readonly string[] sizeOrders = ["b", "kb", "mb", "gb", "tb"];

    public static string ToByteSize(double byteCount, string format = "{0:0.##} {1}")
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
}