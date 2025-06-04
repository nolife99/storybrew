namespace StorybrewCommon.Subtitles.Parsers;

using System;
using System.IO;
using System.Text;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Util;
using ZLinq;

///<summary> Parsing methods for .ass subtitle files. </summary>
public record AssParser : SubtitleParser
{
    /// <inheritdoc/>
    public SubtitleSet Parse(string path)
    {
        using var stream = File.OpenRead(path);
        return Parse(stream);
    }

    /// <inheritdoc/>
    public SubtitleSet Parse(Stream stream)
    {
        using var lines = ValueList<SubtitleLine>.Create();
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
                                {
                                    using var arguments = TempList<ValueList<char>>.Create();
                                    foreach (var arg in value.Split(',')) arguments.Add(ValueList<char>.Create(value[arg]));

                                    string text;
                                    using (var argsArr = arguments.AsReadOnlySpan()[9..]
                                        .AsValueEnumerable()
                                        .Select(c => c.AsReadOnlySpan().ToString())
                                        .ToArrayPool())
                                        text = string.Join('\n', string.Join(',', argsArr.Span).Split("\\N"));

                                    lines.Add(new(SubtitleParser.ParseTimestamp(arguments[1].AsReadOnlySpan()),
                                        SubtitleParser.ParseTimestamp(arguments[2].AsReadOnlySpan()),
                                        text));

                                    foreach (var arg in arguments) arg.Dispose();

                                    break;
                                }
                            }
                        }); break;
                }
            });

        return new(lines.ToArray());
    }
}