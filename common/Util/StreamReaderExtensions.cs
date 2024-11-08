﻿namespace StorybrewCommon.Util;

using System;
using System.IO;

#pragma warning disable CS1591
public static class StreamReaderExtensions
{
    /// <summary>
    ///     Calls <paramref name="action" /> with the content of a .osu file, until it finds a blank line or reaches the
    ///     end of the file.
    /// </summary>
    public static void ParseSections(this StreamReader reader, Action<string> action)
    {
        while (reader.ReadLine() is { } line)
        {
            line = line.Trim();
            if (!line.StartsWith('[') || !line.EndsWith(']')) continue;
            var sectionName = line[1..^1];
            action(sectionName);
        }
    }

    /// <summary>
    ///     Calls <paramref name="action" /> with the content of a line, until it finds a blank line or reaches the end
    ///     of the file.
    /// </summary>
    public static void ParseSectionLines(this StreamReader reader, Action<string> action, bool trimLines = true)
    {
        while (reader.ReadLine() is { } line)
        {
            if (trimLines) line = line.Trim();
            if (line.Length == 0) return;

            action(line);
        }
    }

    /// <summary>
    ///     Calls <paramref name="action" /> with key and value, until it finds a blank line or reaches the end of the
    ///     file.
    /// </summary>
    public static void ParseKeyValueSection(this StreamReader reader, Action<string, string> action)
        => reader.ParseSectionLines(line =>
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex == -1) throw new InvalidDataException($"{line} is not a key/value");

            var key = line[..separatorIndex].Trim();
            var value = line.AsSpan().Slice(separatorIndex + 1, line.Length - 1 - separatorIndex).Trim();

            action(key, value.ToString());
        });
}