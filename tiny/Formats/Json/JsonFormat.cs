namespace Tiny.Formats.Json;

using System.IO;

public class JsonFormat : Format<JsonTokenType>
{
    static readonly RegexTokenizer<JsonTokenType>.Definition[] definitions =
    [
        new(JsonTokenType.PropertyQuoted,
            """
            "((?:[^"\\]|\\.)*)" *:
            """),
        new(JsonTokenType.WordQuoted,
            """
            "((?:[^"\\]|\\.)*)"
            """),
        new(JsonTokenType.ObjectStart, "{"), new(JsonTokenType.ObjectEnd, "}"), new(JsonTokenType.ArrayStart, "\\["),
        new(JsonTokenType.ArrayEnd, "]"), new(JsonTokenType.ValueSeparator, ","),
        new(JsonTokenType.Property, @"([^\s:,{}\[\]]*) *:"), new(JsonTokenType.Word, @"[^\s:,{}\[\]]+")
    ];

    protected override ITokenizer<JsonTokenType> Tokenizer { get; } =
        new RegexTokenizer<JsonTokenType>(definitions, null);

    protected override ITokenParser<JsonTokenType> TokenParser { get; } = new JsonTokenParser();

    public override void Write(TextWriter writer, TinyToken value) { }
}