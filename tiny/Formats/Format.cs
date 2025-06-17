namespace Tiny.Formats;

using System.Collections.Generic;
using System.IO;

public interface ITokenizer<TToken>
{
    IEnumerable<Token<TToken>> Tokenize(TextReader reader);
}

public interface ITokenParser<TToken>
{
    TinyToken Parse(IEnumerable<Token<TToken>> tokens);
}

public interface IFormat
{
    TinyToken Read(TextReader reader);
    void Write(TextWriter writer, TinyToken value);
}

public abstract class Format<TToken> : IFormat
{
    protected abstract ITokenizer<TToken> Tokenizer { get; }
    protected abstract ITokenParser<TToken> TokenParser { get; }

    public TinyToken Read(TextReader reader) => TokenParser.Parse(Tokenizer.Tokenize(reader));
    public abstract void Write(TextWriter writer, TinyToken value);
}