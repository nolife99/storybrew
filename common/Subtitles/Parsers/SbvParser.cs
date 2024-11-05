﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BrewLib.Util;

namespace StorybrewCommon.Subtitles.Parsers;

///<summary> Parsing methods for .sbv subtitle files. </summary>
public class SbvParser : SubtitleParser
{
    ///<inheritdoc/>
    public SubtitleSet Parse(string path)
    {
        using var stream = Misc.WithRetries(() => File.OpenRead(path));
        return Parse(stream);
    }

    ///<inheritdoc/>
    public SubtitleSet Parse(Stream stream)
    {
        List<SubtitleLine> lines = [];
        foreach (var block in parseBlocks(stream))
        {
            var blockLines = block.Split('\n');
            var timestamps = blockLines[0].Split(',');
            var startTime = SubtitleParser.ParseTimestamp(timestamps[0]);
            var endTime = SubtitleParser.ParseTimestamp(timestamps[1]);
            var text = string.Join('\n', blockLines, 1, blockLines.Length - 1);
            lines.Add(new(startTime, endTime, text));
        }
        return new(lines);
    }

    static IEnumerable<string> parseBlocks(Stream stream)
    {
        using StreamReader reader = new(stream);
        StringBuilder sb = new();

        string line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line.Trim()))
            {
                var block = sb.Trim();
                if (block.Length > 0) yield return block.ToString();
                sb.Clear();
            }
            else sb.AppendLine(line);
        }

        var endBlock = sb.Trim();
        if (endBlock.Length > 0) yield return endBlock.ToString();
    }
}