namespace Tiny.Formats.Yaml;

using System;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class YamlUtil
{
    public static string EscapeString(scoped ReadOnlySpan<char> value)
    {
        using var sb = TempList.Create<char>((int)(value.Length * 1.3f));
        foreach (var c in value)
            switch (c)
            {
                case '\r': sb.AddRange("\\r"); break;
                case '\n': sb.AddRange("\\n"); break;
                case '"': sb.AddRange("\\\""); break;
                case '\\': sb.AddRange(@"\\"); break;
                default: sb.Add(c); break;
            }

        return sb.AsReadOnlySpan().ToString();
    }

    public static string UnescapeString(scoped ReadOnlySpan<char> value)
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