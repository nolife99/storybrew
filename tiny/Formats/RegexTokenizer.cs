namespace Tiny.Formats;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class RegexTokenizer<TokenType>(IEnumerable<RegexTokenizer<TokenType>.Definition> definitions, TokenType? endLineToken)
    : Tokenizer<TokenType> where TokenType : struct
{
    public IEnumerable<Token<TokenType>> Tokenize(TextReader reader)
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

    public IEnumerable<Token<TokenType>> Tokenize(string content)
    {
        var matches = definitions.SelectMany((d, i) => d.FindMatches(content, i));
        var byStartGroups = matches.GroupBy(m => m.StartIndex).OrderBy(g => g.Key);

        Definition.Match previousMatch = null;
        foreach (var byStartGroup in byStartGroups)
        {
            var bestMatch = byStartGroup.OrderBy(m => m.Priority).First();
            if (previousMatch is not null && bestMatch.StartIndex < previousMatch.EndIndex) continue;

            yield return new(bestMatch.Type, bestMatch.Value) { CharNumber = bestMatch.StartIndex };

            previousMatch = bestMatch;
        }

        if (endLineToken.HasValue) yield return new(endLineToken.Value);
    }

    public class Definition(TokenType matchType, string regexPattern, int captureGroup = 1)
    {
        readonly Regex regex = new(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IEnumerable<Match> FindMatches(string input, int priority)
        {
            var matches = regex.Matches(input);
            foreach (System.Text.RegularExpressions.Match match in matches)
                yield return new()
                {
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length,
                    Priority = priority,
                    Type = matchType,
                    Value = match.Groups.Count > captureGroup ? match.Groups[captureGroup].Value : match.Value
                };
        }

        public override string ToString() => $"regex:{regex}, matchType:{matchType}, captureGroup:{captureGroup}";

        public class Match
        {
            public int StartIndex, EndIndex, Priority;
            public TokenType Type;
            public string Value;

            public override string ToString() => $"{Type} <{Value}> from {StartIndex} to {EndIndex}, priority:{Priority}";
        }
    }
}