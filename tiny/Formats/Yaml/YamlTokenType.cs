namespace Tiny.Formats.Yaml;

public enum YamlTokenType : byte
{
    Indent, PropertyQuoted, Property, WordQuoted, Word, ArrayIndicator, EndLine
}