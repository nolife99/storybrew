namespace Tiny.Formats.Json;

using System.Text;

public class JsonUtil
{
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