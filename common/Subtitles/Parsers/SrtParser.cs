namespace StorybrewCommon.Subtitles.Parsers;

using System.Collections.Generic;
using System.IO;
using System.Text;
using BrewLib.Util;

///<summary> Parsing methods for .srt subtitle files. </summary>
public class SrtParser : SubtitleParser
{
    /// <inheritdoc/>
    public SubtitleSet Parse(string path)
    {
        using var stream = Misc.WithRetries(() => File.OpenRead(path));
        return Parse(stream);
    }

    /// <inheritdoc/>
    public SubtitleSet Parse(Stream stream)
    {
        List<SubtitleLine> lines = [];
        foreach (var block in parseBlocks(stream))
        {
            var blockLines = block.Split('\n');
            var timestamps = blockLines[1].Split("-->");
            var startTime = SubtitleParser.ParseTimestamp(timestamps[0].Replace(',', '.'));
            var endTime = SubtitleParser.ParseTimestamp(timestamps[1].Replace(',', '.'));
            var text = string.Join("\n", blockLines, 2, blockLines.Length - 2);
            lines.Add(new(startTime, endTime, text));
        }

        return new(lines);
    }

    static IEnumerable<string> parseBlocks(Stream stream)
    {
        using StreamReader reader = new(stream);
        StringBuilder sb = new();

        while (reader.ReadLine() is { } line)
            if (string.IsNullOrEmpty(line.Trim()))
            {
                var block = sb.Trim();
                if (block.Length > 0) yield return block.ToString();
                sb.Clear();
            }
            else sb.AppendLine(line);

        var endBlock = sb.Trim();
        if (endBlock.Length > 0) yield return endBlock.ToString();
    }
}