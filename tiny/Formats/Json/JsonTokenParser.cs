namespace Tiny.Formats.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public partial class JsonTokenParser : TokenParser<JsonTokenType>
{
    public TinyToken Parse(IEnumerable<Token<JsonTokenType>> tokens)
    {
        TinyToken result = null;

        using ParseContext<JsonTokenType> context = new(tokens, new AnyParser(r => result = r));
        while (context.CurrentToken is not null) context.Parser.Parse(context);

        return result;
    }

    class ObjectParser : Parser<JsonTokenType>
    {
        readonly TinyObject result = [];
        bool expectingSeparator;

        public ObjectParser(Action<TinyToken> callback) : base(callback, 0) => callback(result);

        public override void Parse(ParseContext<JsonTokenType> context)
        {
            switch (context.CurrentToken.Type)
            {
                case JsonTokenType.Property:
                case JsonTokenType.PropertyQuoted:

                    if (expectingSeparator)
                        throw new InvalidDataException("Unexpected token: " +
                            context.LookaheadToken +
                            ", after: " +
                            context.CurrentToken);

                    var key = context.CurrentToken.Value;
                    if (context.CurrentToken.Type == JsonTokenType.PropertyQuoted) key = JsonUtil.UnescapeString(key);

                    switch (context.LookaheadToken.Type)
                    {
                        case JsonTokenType.ObjectStart:
                        case JsonTokenType.ArrayStart: context.PushParser(new AnyParser(r => result.Add(key, r))); break;
                        case JsonTokenType.Word:
                        case JsonTokenType.WordQuoted: context.PushParser(new ValueParser(r => result.Add(key, r))); break;
                        default:
                            throw new InvalidDataException("Unexpected token: " +
                                context.LookaheadToken +
                                ", after: " +
                                context.CurrentToken);
                    }

                    expectingSeparator = true;
                    context.ConsumeToken();
                    return;

                case JsonTokenType.ValueSeparator:
                    expectingSeparator = false;
                    context.ConsumeToken();
                    return;

                case JsonTokenType.ObjectEnd:
                    context.ConsumeToken();
                    context.PopParser();
                    return;
            }

            throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
        }

        public override void End() { }
    }

    class ArrayParser : Parser<JsonTokenType>
    {
        readonly TinyArray result = [];
        bool expectingSeparator;

        public ArrayParser(Action<TinyToken> callback) : base(callback, 0) => callback(result);

        public override void Parse(ParseContext<JsonTokenType> context)
        {
            switch (context.CurrentToken.Type)
            {
                case JsonTokenType.ObjectStart:
                case JsonTokenType.ArrayStart:
                case JsonTokenType.Word:
                case JsonTokenType.WordQuoted:
                    if (expectingSeparator) throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
                    context.PushParser(new AnyParser(result.Add));
                    expectingSeparator = true;
                    return;

                case JsonTokenType.ValueSeparator:
                    expectingSeparator = false;
                    context.ConsumeToken();
                    return;

                case JsonTokenType.ArrayEnd:
                    context.ConsumeToken();
                    context.PopParser();
                    return;
            }

            throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
        }

        public override void End() { }
    }

    partial class ValueParser(Action<TinyToken> callback) : Parser<JsonTokenType>(callback, 0)
    {
        public override void Parse(ParseContext<JsonTokenType> context)
        {
            switch (context.CurrentToken.Type)
            {
                case JsonTokenType.Word:
                {
                    var value = context.CurrentToken.Value;
                    if (floatRegex().Match(value).Success) Callback(new TinyValue(value, TinyTokenType.Float));
                    else if (integerRegex().Match(value).Success) Callback(new TinyValue(value, TinyTokenType.Integer));
                    else if (boolRegex().Match(value).Success)
                        Callback(new TinyValue(value.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase)));
                    else Callback(new TinyValue(value));

                    context.ConsumeToken();
                    context.PopParser();
                }

                    return;

                case JsonTokenType.WordQuoted:
                {
                    var value = JsonUtil.UnescapeString(context.CurrentToken.Value);
                    Callback(new TinyValue(value));
                    context.ConsumeToken();
                    context.PopParser();
                }

                    return;
            }

            throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
        }

        public override void End() { }

        [GeneratedRegex("^[-+]?[0-9]*\\.[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
        private static partial Regex floatRegex();

        [GeneratedRegex("^[-+]?\\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
        private static partial Regex integerRegex();

        [GeneratedRegex("^True|False$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
        private static partial Regex boolRegex();
    }

    class AnyParser(Action<TinyToken> callback) : Parser<JsonTokenType>(callback, 0)
    {
        public override void Parse(ParseContext<JsonTokenType> context)
        {
            switch (context.CurrentToken.Type)
            {
                case JsonTokenType.ObjectStart:
                    context.ReplaceParser(new ObjectParser(Callback));
                    context.ConsumeToken();
                    return;

                case JsonTokenType.ArrayStart:
                    context.ReplaceParser(new ArrayParser(Callback));
                    context.ConsumeToken();
                    return;

                case JsonTokenType.Word:
                case JsonTokenType.WordQuoted:
                    context.ReplaceParser(new ValueParser(Callback));
                    return;
            }

            throw new InvalidDataException("Unexpected token: " + context.CurrentToken);
        }

        public override void End() { }
    }
}