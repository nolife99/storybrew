namespace Tiny.Formats.Yaml;

using System;
using System.Globalization;
using System.IO;

public class YamlFormat : Format<YamlTokenType>
{
    public const string BooleanTrue = "Yes", BooleanFalse = "No";

    static readonly RegexTokenizer<YamlTokenType>.Definition[] definitions =
    [
        new(YamlTokenType.Indent, "^(  )+", 0),
        new(YamlTokenType.PropertyQuoted, @"""((?:[^""\\]|\\.)*)"" *:"),
        new(YamlTokenType.WordQuoted, @"""((?:[^""\\]|\\.)*)"""),
        new(YamlTokenType.ArrayIndicator, "- "),
        new(YamlTokenType.Property, "([^\\s:-][^\\s:]*) *:"),
        new(YamlTokenType.Word, "[^\\s:]+"),
        new(YamlTokenType.EndLine, "\n")
    ];

    protected override Tokenizer<YamlTokenType> Tokenizer { get; } =
        new RegexTokenizer<YamlTokenType>(definitions, YamlTokenType.EndLine);

    protected override TokenParser<YamlTokenType> TokenParser { get; } = new YamlTokenParser();

    public override void Write(TextWriter writer, TinyToken value) => write(writer, value, null, 0);

    void write(TextWriter writer, TinyToken token, TinyToken parent, int indentLevel)
    {
        switch (token.Type)
        {
            case TinyTokenType.Object: writeObject(writer, (TinyObject)token, parent, indentLevel); break;
            case TinyTokenType.Array: writeArray(writer, (TinyArray)token, parent, indentLevel); break;
            default: writeValue(writer, (TinyValue)token, parent, indentLevel); break;
        }
    }

    void writeObject(TextWriter writer, TinyObject obj, TinyToken parent, int indentLevel)
    {
        var parentIsArray = parent is not null && parent.Type == TinyTokenType.Array;

        var first = true;
        foreach (var property in obj)
        {
            if (!first || !parentIsArray) writeIndent(writer, indentLevel);

            var key = property.Key;
            if (key.Contains(' ') || key.Contains(':') || key.StartsWith('-'))
                key = string.Concat("\"", YamlUtil.EscapeString(key), "\"");

            var value = property.Value;
            if (value.IsEmpty) writer.WriteLine(key + ":");
            else if (value.IsInline)
            {
                writer.Write(key + ": ");
                write(writer, value, obj, 0);
            }
            else
            {
                writer.WriteLine(key + ":");
                write(writer, value, obj, indentLevel + 1);
            }

            first = false;
        }
    }

    void writeArray(TextWriter writer, TinyArray array, TinyToken parent, int indentLevel)
    {
        var parentIsArray = parent is not null && parent.Type == TinyTokenType.Array;

        var first = true;
        foreach (var token in array)
        {
            if (!first || !parentIsArray) writeIndent(writer, indentLevel);

            if (token.IsEmpty) writer.WriteLine("- ");
            else if (token.IsInline)
            {
                writer.Write("- ");
                write(writer, token, array, 0);
            }
            else
            {
                writer.Write("- ");
                write(writer, token, array, indentLevel + 1);
            }

            first = false;
        }
    }

    static void writeValue(TextWriter writer, TinyValue valueToken, TinyToken parent, int indentLevel)
    {
        if (indentLevel != 0) throw new InvalidOperationException();

        var type = valueToken.Type;
        var value = valueToken.Value<object>();

        switch (type)
        {
            case TinyTokenType.Null: writer.WriteLine(); break;

            case TinyTokenType.String:
                writer.WriteLine(string.Concat("\"", YamlUtil.EscapeString((string)value), "\"")); break;

            case TinyTokenType.Integer: writer.WriteLine(value?.ToString()); break;

            case TinyTokenType.Float:
                switch (value)
                {
                    case float floatFloat: writer.WriteLine(floatFloat.ToString(CultureInfo.InvariantCulture)); break;
                    case double floatDouble: writer.WriteLine(floatDouble.ToString(CultureInfo.InvariantCulture)); break;
                    case decimal floatDecimal: writer.WriteLine(floatDecimal.ToString(CultureInfo.InvariantCulture)); break;
                    case string floatString: writer.WriteLine(floatString); break;
                    default: throw new InvalidDataException(value?.ToString());
                }

            break;

            case TinyTokenType.Boolean: writer.WriteLine((bool)value ? BooleanTrue : BooleanFalse); break;

            case TinyTokenType.Array:
            case TinyTokenType.Object:
            case TinyTokenType.Invalid: throw new InvalidDataException(type.ToString());

            default: throw new NotSupportedException(type.ToString());
        }
    }

    static void writeIndent(TextWriter writer, int indentLevel)
    {
        if (indentLevel <= 0) return;

        writer.Write(new string(' ', indentLevel * 2));
    }
}