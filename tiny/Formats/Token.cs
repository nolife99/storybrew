namespace Tiny.Formats;

public class Token<TToken>(TToken type, string value = null)
{
    public int LineNumber, CharNumber;
    public TToken Type => type;
    public string Value => value;

    public override string ToString() => $"{Type} <{Value}> (line {LineNumber}, char {CharNumber})";
}