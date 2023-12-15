using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BrewLib.Util;

namespace StorybrewCommon.Subtitles.Parsers;

///<summary> Parsing methods for .srt subtitle files. </summary>
public class SrtParser : SubtitleParser
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
            var timestamps = blockLines[1].Split("-->", StringSplitOptions.None);
            var startTime = parseTimestamp(timestamps[0]);
            var endTime = parseTimestamp(timestamps[1]);
            var text = string.Join("\n", blockLines, 2, blockLines.Length - 2);
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
            if (string.IsNullOrEmpty(line.Trim()))
            {
                var block = sb.ToString().Trim();
                if (block.Length > 0) yield return block;
                sb.Clear();
            }
            else sb.AppendLine(line);
        }

        var endBlock = sb.ToString().Trim();
        if (endBlock.Length > 0) yield return endBlock;
    }

    static double parseTimestamp(string timestamp) => TimeSpan.Parse(timestamp.Replace(',', '.'), CultureInfo.InvariantCulture).TotalMilliseconds;
}