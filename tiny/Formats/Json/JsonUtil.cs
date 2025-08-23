namespace Tiny.Formats.Json;

using System;

public static class JsonUtil
{
    public static string UnescapeString(scoped ReadOnlySpan<char> value)
        => string.Create(value.Length,
            value,
            (span, state) =>
            {
                var special = false;
                for (var i = 0; i < state.Length; ++i)
                {
                    var c = state[i];
                    if (special)
                    {
                        span[i] = c switch { 'r' => '\r', 'n' => '\n', _ => c };
                        special = false;
                    }
                    else if (c == '\\') special = true;
                    else span[i] = c;
                }
            });
}