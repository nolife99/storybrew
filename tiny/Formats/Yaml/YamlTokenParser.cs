using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Tiny.Formats.Yaml
{
    public partial class YamlTokenParser : TokenParser<YamlTokenType>
    {
        public TinyToken Parse(IEnumerable<Token<YamlTokenType>> tokens)
        {
            TinyToken result = null;

            var context = new ParseContext<YamlTokenType>(tokens, new AnyParser(r => result = r));
            while (context.CurrentToken != null)
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
                // Debug.Print($"  - {context.Parser.GetType().Name} ({context.ParserCount})");
                context.Parser.Parse(context);
            }
            context.End();

            return result;
        }

        abstract class MultilineParser : Parser<YamlTokenType>
        {
            int? indent;
            protected abstract int ResultCount { get; }

            public MultilineParser(Action<TinyToken> callback, int virtualIndent) : base(callback, virtualIndent) { }

            protected bool CheckIndent(ParseContext<YamlTokenType> context)
            {
                indent = indent ?? context.IndentLevel + VirtualIndent;
                var lineIndent = ResultCount == 0 ? context.IndentLevel + VirtualIndent : context.IndentLevel;
                if (lineIndent != indent)
                {
                    if (lineIndent > indent) throw new InvalidDataException($"Unexpected indent: {lineIndent}, expected: {indent}, token: {context.CurrentToken}");

                    context.PopParser();
                    return true;
                }
                return false;
            }
        }

        class ObjectParser : MultilineParser
        {
            readonly TinyObject result = new();
            protected override int ResultCount => result.Count;

            public ObjectParser(Action<TinyToken> callback, int virtualIndent = 0) : base(callback, virtualIndent) => callback(result);

            public override void Parse(ParseContext<YamlTokenType> context)
            {
                if (CheckIndent(context))
                    return;

                switch (context.LookaheadToken.Type)
                {
                    case YamlTokenType.ArrayIndicator:
                    case YamlTokenType.Property:
                    case YamlTokenType.PropertyQuoted:
                        throw new InvalidDataException("Unexpected token: " + context.LookaheadToken + ", after: " + context.CurrentToken);
                }

                switch (context.CurrentToken.Type)
                {
                    case YamlTokenType.Property:
                    case YamlTokenType.PropertyQuoted:
                        var key = context.CurrentToken.Value;
                        if (context.CurrentToken.Type == YamlTokenType.PropertyQuoted)
                            key = YamlUtil.UnescapeString(key);

                        switch (context.LookaheadToken.Type)
                        {
                            case YamlTokenType.Word:
                            case YamlTokenType.WordQuoted:
                                context.PushParser(new ValueParser(r => result.Add(key, r)));
                                break;
                            case YamlTokenType.EndLine:
                                context.PushParser(new EmptyProperyParser(r => result.Add(key, r), context.IndentLevel + 1));
                                break;
                            default:
                                throw new InvalidDataException("Unexpected token: " + context.LookaheadToken + ", after: " + context.CurrentToken);
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
            readonly TinyArray result = new();
            protected override int ResultCount => result.Count;

            public ArrayParser(Action<TinyToken> callback, int virtualIndent = 0) : base(callback, virtualIndent) => callback(result);

            public override void Parse(ParseContext<YamlTokenType> context)
            {
                if (CheckIndent(context))
                    return;

                switch (context.CurrentToken.Type)
                {
                    case YamlTokenType.ArrayIndicator:
                        context.PushParser(new AnyParser(r => result.Add(r), result.Count == 0 ? VirtualIndent + 1 : 1));
                        context.ConsumeToken();
                        return;
                }

                throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
            }

            public override void End() { }
        }

        class ValueParser : Parser<YamlTokenType>
        {
            static readonly Regex floatRegex = new("^[-+]?[0-9]*\\.[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            static readonly Regex integerRegex = new("^[-+]?\\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            static readonly Regex boolRegex = new($"^{YamlFormat.BooleanTrue}|{YamlFormat.BooleanFalse}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public ValueParser(Action<TinyToken> callback) : base(callback, 0) { }

            public override void Parse(ParseContext<YamlTokenType> context)
            {
                switch (context.LookaheadToken.Type)
                {
                    case YamlTokenType.EndLine: break;
                    default: throw new InvalidDataException("Unexpected token: " + context.LookaheadToken + ", after: " + context.CurrentToken);
                }

                switch (context.CurrentToken.Type)
                {
                    case YamlTokenType.Word:
                    {
                        var value = context.CurrentToken.Value;
                        Match match;
                        if ((match = floatRegex.Match(value)).Success) Callback(new TinyValue(value, TinyTokenType.Float));
                        else if ((match = integerRegex.Match(value)).Success) Callback(new TinyValue(value, TinyTokenType.Integer));
                        else if ((match = boolRegex.Match(value)).Success) Callback(new TinyValue(value.Equals(YamlFormat.BooleanTrue, StringComparison.OrdinalIgnoreCase)));
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

        class AnyParser : Parser<YamlTokenType>
        {
            public AnyParser(Action<TinyToken> callback, int virtualIndent = 0) : base(callback, virtualIndent) { }

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

        class EmptyProperyParser : Parser<YamlTokenType>
        {
            readonly int expectedIndent;

            public EmptyProperyParser(Action<TinyToken> callback, int expectedIndent, int virtualIndent = 0) : base(callback, virtualIndent)
                => this.expectedIndent = expectedIndent;

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

                throw new InvalidDataException($"Unexpected indent: {context.IndentLevel}, expected: {expectedIndent}, token: {context.CurrentToken}");
            }

            public override void End() => Callback(new TinyValue(null, TinyTokenType.Null));
        }
    }
}