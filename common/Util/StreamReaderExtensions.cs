namespace StorybrewCommon.Util;

using System;
using System.IO;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

/// <summary>Contains extension methods for <see cref="StreamReader"/> to parse .osu sections and section lines.</summary>
public static class StreamReaderExtensions
{
    /// <summary>
    ///     Calls <paramref name="action"/> with the content of a .osu file, until it finds a blank line or reaches the end of
    ///     the file.
    /// </summary>
    public static void ParseSections(this StreamReader reader, Action<ReadOnlySpan<char>> action)
    {
        while (ReadLine(reader, out var read))
            using (read)
            {
                var line = read.AsReadOnlySpan().Trim();
                if (line.Length == 0 || line[0] != '[' || line[^1] != ']') continue;

                action(line[1..^1]);
            }
    }

    /// <summary>
    ///     Calls <paramref name="action"/> with the content of a line, until it finds a blank line or reaches the end of the
    ///     file.
    /// </summary>
    public static void ParseSectionLines(this StreamReader reader, Action<ReadOnlySpan<char>> action, bool trimLines = true)
    {
        while (ReadLine(reader, out var read))
            using (read)
            {
                var line = read.AsReadOnlySpan();
                if (trimLines) line = line.Trim();
                if (line.Length == 0) return;

                action(line);
            }
    }

    /// <summary>Calls <paramref name="action"/> with key and value, until it finds a blank line or reaches the end of the file.</summary>
    public static void ParseKeyValueSection(this StreamReader reader, Action<ReadOnlySpan<char>, ReadOnlySpan<char>> action)
    {
        while (ReadLine(reader, out var read))
            using (read)
            {
                var line = read.AsReadOnlySpan().Trim();
                if (line.Length == 0) return;

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex == -1) throw new InvalidDataException($"{line} is not a key/value");

                action(line[..separatorIndex].Trim(), line[(separatorIndex + 1)..].Trim());
            }
    }

    static bool ReadLine(StreamReader reader, out TempList<char> list)
    {
        list = TempList<char>.Create();

        if (reader.EndOfStream) return false;

        int charRead;
        var foundEndOfLine = false;

        while (!foundEndOfLine && (charRead = reader.Read()) != -1)
        {
            var c = (char)charRead;
            switch (c)
            {
                case '\r':
                {
                    if (reader.Peek() == '\n') reader.Read();

                    foundEndOfLine = true;
                    break;
                }

                case '\n': foundEndOfLine = true; break;

                default: list.Add(c); break;
            }
        }

        return true;
    }
}