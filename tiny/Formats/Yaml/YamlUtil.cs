namespace Tiny.Formats.Yaml;

using System.Text;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class YamlUtil
{
    public static string EscapeString(string value)
    {
        StringBuilder sb = new((int)(value.Length * 1.3f));
        foreach (var c in value)
            switch (c)
            {
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append(@"\\"); break;
                default: sb.Append(c); break;
            }

        return sb.ToString();
    }

    public static string UnescapeString(string value)
    {
        var special = false;

        using var sb = TempList.Create<char>(value.Length);
        foreach (var c in value)
            if (special)
            {
                switch (c)
                {
                    case 'r': sb.Add('\r'); break;
                    case 'n': sb.Add('\n'); break;
                    default: sb.Add(c); break;
                }

                special = false;
            }
            else if (c == '\\') special = true;
            else sb.Add(c);

        return sb.AsReadOnlySpan().ToString();
    }
}