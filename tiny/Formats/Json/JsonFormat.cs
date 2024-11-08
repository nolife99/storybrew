namespace Tiny.Formats.Json;

using System.IO;

public class JsonFormat : Format<JsonTokenType>
{
    public const string BooleanTrue = "true", BooleanFalse = "false";

    static readonly RegexTokenizer<JsonTokenType>.Definition[] definitions =
    [
        new(JsonTokenType.PropertyQuoted, @"""((?:[^""\\]|\\.)*)"" *:"),
        new(JsonTokenType.WordQuoted, @"""((?:[^""\\]|\\.)*)"""), new(JsonTokenType.ObjectStart, "{"),
        new(JsonTokenType.ObjectEnd, "}"), new(JsonTokenType.ArrayStart, "\\["), new(JsonTokenType.ArrayEnd, "]"),
        new(JsonTokenType.ValueSeparator, ","), new(JsonTokenType.Property, "([^\\s:,{}\\[\\]]*) *:"),
        new(JsonTokenType.Word, "[^\\s:,{}\\[\\]]+")
    ];

    protected override Tokenizer<JsonTokenType> Tokenizer { get; } =
        new RegexTokenizer<JsonTokenType>(definitions, null);

    protected override TokenParser<JsonTokenType> TokenParser { get; } = new JsonTokenParser();

    public override void Write(TextWriter writer, TinyToken value) { }
}