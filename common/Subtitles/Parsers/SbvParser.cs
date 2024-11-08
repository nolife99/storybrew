namespace StorybrewCommon.Subtitles.Parsers;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BrewLib.Util;

///<summary> Parsing methods for .sbv subtitle files. </summary>
public class SbvParser : SubtitleParser
{
    /// <inheritdoc />
    public SubtitleSet Parse(string path)
    {
        using var stream = Misc.WithRetries(() => File.OpenRead(path));
        return Parse(stream);
    }

    /// <inheritdoc />
    public SubtitleSet Parse(Stream stream)
    {
        List<SubtitleLine> lines = [];
        lines.AddRange(from block in parseBlocks(stream)
            select block.Split('\n')
            into blockLines
            let timestamps = blockLines[0].Split(',')
            let startTime = SubtitleParser.ParseTimestamp(timestamps[0])
            let endTime = SubtitleParser.ParseTimestamp(timestamps[1])
            let text = string.Join('\n', blockLines, 1, blockLines.Length - 1)
            select new SubtitleLine(startTime, endTime, text));

        return new(lines);
    }

    static IEnumerable<string> parseBlocks(Stream stream)
    {
        using StreamReader reader = new(stream);
        StringBuilder sb = new();

        while (reader.ReadLine() is { } line)
            if (string.IsNullOrWhiteSpace(line.Trim()))
            {
                var block = sb.Trim();
                if (block.Length > 0) yield return block.ToString();
                sb.Clear();
            }
            else
                sb.AppendLine(line);

        var endBlock = sb.Trim();
        if (endBlock.Length > 0) yield return endBlock.ToString();
    }
}