namespace StorybrewCommon.Subtitles.Parsers;

using System.Collections.Generic;
using System.IO;
using System.Text;
using BrewLib.Util;
using ZLinq;

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
    public SubtitleSet Parse(Stream stream)
        => new(parseBlocks(stream)
            .AsValueEnumerable()
            .Select(block => block.Split('\n'))
            .Select(blockLines => (blockLines, blockLines[0].Split(',')))
            .Select(t => new SubtitleLine(SubtitleParser.ParseTimestamp(t.Item2[0]),
                SubtitleParser.ParseTimestamp(t.Item2[1]),
                string.Join('\n', t.blockLines, 1, t.blockLines.Length - 1)))
            .ToArray());

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