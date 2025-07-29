namespace Tiny.Formats;

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Tiny.PooledCollections.Generic.Temporary;
using ZLinq;

public class RegexTokenizer<TTokenType>(RegexTokenizer<TTokenType>.Definition[] definitions, TTokenType? endLineToken)
    : ITokenizer<TTokenType> where TTokenType : struct
{
    TempList<Token<TTokenType>> ITokenizer<TTokenType>.Tokenize(TextReader reader)
    {
        var result = TempList.Create<Token<TTokenType>>();
        var lineNumber = 1;

        while (reader.ReadLine() is { } line)
        {
            using var tokens = Tokenize(line);
            foreach (var token in tokens)
            {
                token.LineNumber = lineNumber;
                result.Add(token);
            }

            ++lineNumber;
        }

        return result;
    }

    TempList<Token<TTokenType>> Tokenize(string content)
    {
        var result = TempList.Create<Token<TTokenType>>();
        if (content is null) return result;

        using var enumerator = definitions.AsValueEnumerable()
            .SelectMany((d, i) => d.regex.Value.Matches(content)
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
            .Enumerator;

        if (!enumerator.TryGetNext(out var current)) return result;

        Definition.Match previousMatch = default;
        while (true)
            if (previousMatch.Value is null || current.StartIndex >= previousMatch.EndIndex)
            {
                var skip = false;
                var next = current;

                while (enumerator.TryGetNext(out next))
                {
                    if (next.StartIndex != current.StartIndex) break;

                    if (next.Priority >= current.Priority) continue;

                    skip = true;
                    break;
                }

                if (!skip)
                {
                    result.Add(new(current.Type, current.Value) { CharNumber = current.StartIndex });
                    previousMatch = current;
                }

                if (next.StartIndex == current.StartIndex) continue;

                current = next;
            }
            else if (!enumerator.TryGetNext(out current)) break;

        if (endLineToken.HasValue) result.Add(new(endLineToken.Value));

        return result;
    }

    public sealed class Definition(TTokenType matchType, string regexPattern, int captureGroup = 1)
    {
        internal readonly int captureGroup = captureGroup;
        internal readonly TTokenType matchType = matchType;

        internal readonly Lazy<Regex> regex = new(() => new(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled),
            LazyThreadSafetyMode.None);

        internal readonly struct Match
        {
            public int StartIndex { get; init; }
            public int EndIndex { get; init; }
            public int Priority { get; init; }
            public TTokenType Type { get; init; }
            public string Value { get; init; }
        }
    }
}