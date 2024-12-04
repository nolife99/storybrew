namespace StorybrewCommon.Util;

using System;
using System.IO;

#pragma warning disable CS1591
public static class StreamReaderExtensions
{
    /// <summary>
    ///     Calls <paramref name="action"/> with the content of a .osu file, until it finds a blank line or reaches the
    ///     end of the file.
    /// </summary>
    public static void ParseSections(this StreamReader reader, Action<string> action)
    {
        while (reader.ReadLine() is { } l)
        {
            var line = l.AsSpan().Trim();
            if (line.Length == 0 || line[0] != '[' || line[^1] != ']') continue;
            action(line[1..^1].ToString());
        }
    }
    /// <summary>
    ///     Calls <paramref name="action"/> with the content of a line, until it finds a blank line or reaches the end
    ///     of the file.
    /// </summary>
    public static void ParseSectionLines(this StreamReader reader, Action<string> action, bool trimLines = true)
    {
        while (reader.ReadLine() is { } l)
        {
            var line = l.AsSpan();
            if (trimLines) line = line.Trim();
            if (line.Length == 0) return;

            action(trimLines ? line.ToString() : l);
        }
    }
    /// <summary>
    ///     Calls <paramref name="action"/> with key and value, until it finds a blank line or reaches the end of the
    ///     file.
    /// </summary>
    public static void ParseKeyValueSection(this StreamReader reader, Action<string, string> action) => reader.ParseSectionLines(l
        =>
    {
        var line = l.AsSpan();
        var separatorIndex = line.IndexOf(':');
        if (separatorIndex == -1) throw new InvalidDataException($"{line} is not a key/value");

        action(line[..separatorIndex].Trim().ToString(), line[(separatorIndex + 1)..].Trim().ToString());
    });
}