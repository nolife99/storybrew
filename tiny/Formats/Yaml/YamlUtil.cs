﻿namespace Tiny.Formats.Yaml;

using System.Text;

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
                case '\\': sb.Append("\\\\"); break;
                default: sb.Append(c); break;
            }

        return sb.ToString();
    }

    public static string UnescapeString(string value)
    {
        var special = false;

        StringBuilder sb = new(value.Length);
        foreach (var c in value)
            if (special)
            {
                switch (c)
                {
                    case 'r': sb.Append('\r'); break;
                    case 'n': sb.Append('\n'); break;
                    default: sb.Append(c); break;
                }

                special = false;
            }
            else if (c == '\\') special = true;
            else sb.Append(c);

        return sb.ToString();
    }
}