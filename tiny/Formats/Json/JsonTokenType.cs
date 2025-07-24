namespace Tiny.Formats.Json;

public enum JsonTokenType : byte
{
    PropertyQuoted, WordQuoted, ObjectStart, ObjectEnd, ArrayStart, ArrayEnd, ValueSeparator, Property, Word
}