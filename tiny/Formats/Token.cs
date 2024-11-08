namespace Tiny.Formats;

public class Token<TokenType>(TokenType type, string value = null)
{
    public int LineNumber, CharNumber;
    public TokenType Type = type;
    public string Value = value;

    public override string ToString() => $"{Type} <{Value}> (line {LineNumber}, char {CharNumber})";
}