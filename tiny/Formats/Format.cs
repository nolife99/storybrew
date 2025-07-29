namespace Tiny.Formats;

using System;
using System.IO;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public interface ITokenizer<TToken>
{
    internal TempList<Token<TToken>> Tokenize(TextReader reader);
}

public interface ITokenParser<TToken>
{
    TinyToken Parse(ReadOnlySpan<Token<TToken>> tokens);
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

    public TinyToken Read(TextReader reader)
    {
        using var tokens = Tokenizer.Tokenize(reader);
        return TokenParser.Parse(tokens.AsReadOnlySpan());
    }

    public abstract void Write(TextWriter writer, TinyToken value);
}