namespace Tiny.Formats;

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ZLinq;

public class RegexTokenizer<TTokenType>(IEnumerable<RegexTokenizer<TTokenType>.Definition> definitions,
    TTokenType? endLineToken) : ITokenizer<TTokenType> where TTokenType : struct
{
    public IEnumerable<Token<TTokenType>> Tokenize(TextReader reader)
    {
        var lineNumber = 1;

        while (reader.ReadLine() is { } line)
        {
            foreach (var token in Tokenize(line))
            {
                token.LineNumber = lineNumber;
                yield return token;
            }

            ++lineNumber;
        }
    }

    IEnumerable<Token<TTokenType>> Tokenize(string content)
    {
        if (content == null) yield break;

        // TODO: Change to allocation-less
        /* using PooledList<Definition.Match> matches = new();
        foreach (var def in definitions)
        {
            var matchColl = def.regex.Matches(content);
            for (var i = 0; i < matchColl.Count; ++i)
            {
                var match = matchColl[i];
                matches.Add(new()
                {
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Priority = i,
                    Type = def.matchType,
                    Value = match.Groups.Count > def.captureGroup ? match.Groups[def.captureGroup].Value : match.Value
                });
            }
        }
        matches.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex)); */

        using var matches = definitions.AsValueEnumerable()
            .SelectMany((d, i) => d.regex.Matches(content)
                .AsValueEnumerable()
                .Select(match => new Definition.Match
                {
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Priority = i,
                    Type = d.matchType,
                    Value = match.Groups.Count > d.captureGroup ? match.Groups[d.captureGroup].Value : match.Value
                }))
            .OrderBy(d => d.StartIndex)
            .ToArrayPool();

        var matchArr = matches.Array;

        Definition.Match previousMatch = default;
        for (var i = 0; i < matches.Size; ++i)
        {
            var current = matchArr[i];

            if (previousMatch.Value is not null && current.StartIndex < previousMatch.EndIndex) continue;

            if (i + 1 < matches.Size &&
                matchArr[i + 1].StartIndex == current.StartIndex &&
                matchArr[i + 1].Priority < current.Priority) continue;

            yield return new(current.Type, current.Value) { CharNumber = current.StartIndex };

            previousMatch = current;
        }

        if (endLineToken.HasValue) yield return new(endLineToken.Value);
    }

    public sealed class Definition(TTokenType matchType, string regexPattern, int captureGroup = 1)
    {
        internal readonly int captureGroup = captureGroup;
        internal readonly TTokenType matchType = matchType;
        internal readonly Regex regex = new(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal readonly record struct Match
        {
            public int StartIndex { get; init; }
            public int EndIndex { get; init; }
            public int Priority { get; init; }
            public TTokenType Type { get; init; }
            public string Value { get; init; }
        }
    }
}