namespace StorybrewCommon.Subtitles.Parsers;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BrewLib.Util;
using Util;

///<summary> Parsing methods for .ass subtitle files. </summary>
public class AssParser : SubtitleParser
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
        using (StreamReader reader = new(stream, Encoding.ASCII))
            reader.ParseSections(sectionName =>
            {
                switch (sectionName)
                {
                    case "Events":
                        reader.ParseKeyValueSection((key, value) =>
                        {
                            switch (key)
                            {
                                case "Dialogue":
                                    var arguments = value.Split(',');
                                    var startTime = SubtitleParser.ParseTimestamp(arguments[1]);
                                    var endTime = SubtitleParser.ParseTimestamp(arguments[2]);
                                    var text = string.Join("\n", string.Join(",", arguments.Skip(9)).Split("\\N"));
                                    lines.Add(new(startTime, endTime, text));
                                    break;
                            }
                        });

                        break;
                }
            });

        return new(lines);
    }
}