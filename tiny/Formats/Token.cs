namespace Tiny.Formats;

using System;

public struct Token<TToken>(TToken type, ReadOnlyMemory<char> value = default)
{
    public int LineNumber, CharNumber;
    public TToken Type => type;
    public ReadOnlyMemory<char> Value => value;

    public override string ToString() => $"{Type} <{Value}> (line {LineNumber}, char {CharNumber})";
}