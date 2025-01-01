namespace Tiny.Formats.Yaml;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class YamlTokenParser : TokenParser<YamlTokenType>
{
    public TinyToken Parse(IEnumerable<Token<YamlTokenType>> tokens)
    {
        TinyToken result = null;

        using ParseContext<YamlTokenType> context = new(tokens, new AnyParser(r => result = r));
        while (context.CurrentToken is not null)
        {
            switch (context.CurrentToken.Type)
            {
                case YamlTokenType.Indent:
                    context.Indent(context.CurrentToken.Value.Length / 2);
                    context.ConsumeToken();
                    continue;

                case YamlTokenType.EndLine:
                    context.NewLine();
                    context.ConsumeToken();
                    continue;
            }

            context.Parser.Parse(context);
        }

        return result;
    }

    abstract class MultilineParser(Action<TinyToken> callback, int virtualIndent) : Parser<YamlTokenType>(
        callback,
        virtualIndent)
    {
        int? indent;
        protected abstract int ResultCount { get; }

        protected bool CheckIndent(ParseContext<YamlTokenType> context)
        {
            indent ??= context.IndentLevel + VirtualIndent;
            var lineIndent = ResultCount == 0 ? context.IndentLevel + VirtualIndent : context.IndentLevel;
            if (lineIndent != indent)
            {
                if (lineIndent > indent)
                    throw new InvalidDataException(
                        $"Unexpected indent: {lineIndent}, expected: {indent}, token: {
                            context.CurrentToken}");

                context.PopParser();
                return true;
            }

            return false;
        }
    }

    class ObjectParser : MultilineParser
    {
        readonly TinyObject result = [];

        public ObjectParser(Action<TinyToken> callback, int virtualIndent = 0) : base(callback, virtualIndent)
            => callback(result);

        protected override int ResultCount => result.Count;

        public override void Parse(ParseContext<YamlTokenType> context)
        {
            if (CheckIndent(context)) return;

            switch (context.LookaheadToken.Type)
            {
                case YamlTokenType.ArrayIndicator:
                case YamlTokenType.Property:
                case YamlTokenType.PropertyQuoted:
                    throw new InvalidDataException(
                        "Unexpected token: " + context.LookaheadToken + ", after: " + context.CurrentToken);
            }

            switch (context.CurrentToken.Type)
            {
                case YamlTokenType.Property:
                case YamlTokenType.PropertyQuoted:
                    var key = context.CurrentToken.Value;
                    if (context.CurrentToken.Type == YamlTokenType.PropertyQuoted) key = YamlUtil.UnescapeString(key);

                    switch (context.LookaheadToken.Type)
                    {
                        case YamlTokenType.Word:
                        case YamlTokenType.WordQuoted: context.PushParser(new ValueParser(r => result.Add(key, r))); break;

                        case YamlTokenType.EndLine:
                            context.PushParser(new EmptyProperyParser(r => result.Add(key, r), context.IndentLevel + 1));
                        break;

                        default:
                            throw new InvalidDataException(
                                "Unexpected token: " + context.LookaheadToken + ", after: " + context.CurrentToken);
                    }

                    context.ConsumeToken();
                    return;
            }

            throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
        }

        public override void End() { }
    }

    class ArrayParser : MultilineParser
    {
        readonly TinyArray result = [];

        public ArrayParser(Action<TinyToken> callback, int virtualIndent = 0) : base(callback, virtualIndent)
            => callback(result);

        protected override int ResultCount => result.Count;

        public override void Parse(ParseContext<YamlTokenType> context)
        {
            if (CheckIndent(context)) return;

            switch (context.CurrentToken.Type)
            {
                case YamlTokenType.ArrayIndicator:
                    context.PushParser(new AnyParser(result.Add, result.Count == 0 ? VirtualIndent + 1 : 1));
                    context.ConsumeToken();
                    return;
            }

            throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
        }

        public override void End() { }
    }

    class ValueParser(Action<TinyToken> callback) : Parser<YamlTokenType>(callback, 0)
    {
        static readonly Regex floatRegex = new("^[-+]?[0-9]*\\.[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            integerRegex = new("^[-+]?\\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            boolRegex = new(
                $"^{YamlFormat.BooleanTrue}|{YamlFormat.BooleanFalse}$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public override void Parse(ParseContext<YamlTokenType> context)
        {
            switch (context.LookaheadToken.Type)
            {
                case YamlTokenType.EndLine: break;

                default:
                    throw new InvalidDataException(
                        "Unexpected token: " + context.LookaheadToken + ", after: " + context.CurrentToken);
            }

            switch (context.CurrentToken.Type)
            {
                case YamlTokenType.Word:
                {
                    var value = context.CurrentToken.Value;
                    Match match;
                    if ((match = floatRegex.Match(value)).Success) Callback(new TinyValue(value, TinyTokenType.Float));
                    else if ((match = integerRegex.Match(value)).Success)
                        Callback(new TinyValue(value, TinyTokenType.Integer));
                    else if ((match = boolRegex.Match(value)).Success)
                        Callback(new TinyValue(value.Equals(YamlFormat.BooleanTrue, StringComparison.OrdinalIgnoreCase)));
                    else Callback(new TinyValue(value));

                    context.ConsumeToken();
                    context.PopParser();
                }

                    return;

                case YamlTokenType.WordQuoted:
                {
                    var value = YamlUtil.UnescapeString(context.CurrentToken.Value);
                    Callback(new TinyValue(value));
                    context.ConsumeToken();
                    context.PopParser();
                }

                    return;
            }

            throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
        }

        public override void End() { }
    }

    class AnyParser(Action<TinyToken> callback, int virtualIndent = 0) : Parser<YamlTokenType>(callback, virtualIndent)
    {
        public override void Parse(ParseContext<YamlTokenType> context)
        {
            switch (context.CurrentToken.Type)
            {
                case YamlTokenType.Property:
                case YamlTokenType.PropertyQuoted:
                    context.ReplaceParser(new ObjectParser(Callback, VirtualIndent));
                    return;

                case YamlTokenType.ArrayIndicator:
                    context.ReplaceParser(new ArrayParser(Callback, VirtualIndent));
                    return;

                case YamlTokenType.Word:
                case YamlTokenType.WordQuoted:
                    context.ReplaceParser(new ValueParser(Callback));
                    return;
            }

            throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
        }

        public override void End() { }
    }

    class EmptyProperyParser(Action<TinyToken> callback, int expectedIndent, int virtualIndent = 0) : Parser<YamlTokenType>(
        callback,
        virtualIndent)
    {
        readonly int expectedIndent = expectedIndent;

        public override void Parse(ParseContext<YamlTokenType> context)
        {
            if (context.IndentLevel < expectedIndent)
            {
                Callback(new TinyValue(null, TinyTokenType.Null));
                context.PopParser();
                return;
            }

            if (context.IndentLevel == expectedIndent)
            {
                context.ReplaceParser(new AnyParser(Callback));
                return;
            }

            throw new InvalidDataException(
                $"Unexpected indent: {context.IndentLevel}, expected: {expectedIndent
                }, token: {context.CurrentToken}");
        }

        public override void End() => Callback(new TinyValue(null, TinyTokenType.Null));
    }
}