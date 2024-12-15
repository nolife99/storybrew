namespace StorybrewCommon.Subtitles.Parsers;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BrewLib.Util;

///<summary> Parsing methods for .sbv subtitle files. </summary>
public record SbvParser : SubtitleParser
{
    /// <inheritdoc/>
    public SubtitleSet Parse(string path)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream);
    }

    /// <inheritdoc/>
    public SubtitleSet Parse(Stream stream) => new(from block in parseBlocks(stream)
        select block.Split('\n') into blockLines
        let timestamps = blockLines[0].Split(',')
        select new SubtitleLine(SubtitleParser.ParseTimestamp(timestamps[0]),
            SubtitleParser.ParseTimestamp(timestamps[1]),
            string.Join('\n', blockLines, 1, blockLines.Length - 1)));

    static IEnumerable<string> parseBlocks(Stream stream)
    {
        using StreamReader reader = new(stream);
        StringBuilder sb = new();

        while (reader.ReadLine() is { } line)
            if (string.IsNullOrWhiteSpace(line.Trim()))
            {
                var block = sb.TrimEnd();
                if (block.Length > 0) yield return block.ToString();
                sb.Clear();
            }
            else sb.AppendLine(line);

        var endBlock = sb.TrimEnd();
        if (endBlock.Length > 0) yield return endBlock.ToString();
    }
}