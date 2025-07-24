namespace Tiny.Formats;

using System;
using PooledCollections.Generic.Temporary;

public ref struct ParseContext<TTokenType>
{
    TempStack<Parser<TTokenType>> parserStack;
    ReadOnlySpan<Token<TTokenType>>.Enumerator tokenEnumerator;

    public ParseContext(ReadOnlySpan<Token<TTokenType>> tokens, Parser<TTokenType> initialParser)
    {
        tokenEnumerator = tokens.GetEnumerator();
        initializeCurrentAndLookahead();

        parserStack = TempStack.Create<Parser<TTokenType>>();
        parserStack.Push(initialParser);
    }

    public Token<TTokenType> CurrentToken { get; private set; }
    public Token<TTokenType> LookaheadToken { get; private set; }

    public Parser<TTokenType> Parser => parserStack.Count > 0 ? parserStack.Peek() : null;

    public int IndentLevel { get; private set; }

    public void Dispose()
    {
        while (Parser is not null)
        {
            Parser.End();
            PopParser();
        }

        parserStack.Dispose();
    }

    public void PopParser() => parserStack.Pop();
    public void PushParser(Parser<TTokenType> parser) => parserStack.Push(parser);

    public void ReplaceParser(Parser<TTokenType> parser)
    {
        parserStack.Pop();
        parserStack.Push(parser);
    }

    public void Indent(int level) => IndentLevel = level;
    public void NewLine() => IndentLevel = 0;

    public void ConsumeToken()
    {
        CurrentToken = LookaheadToken;
        LookaheadToken = tokenEnumerator.MoveNext() ? tokenEnumerator.Current : null;
    }

    void initializeCurrentAndLookahead()
    {
        ConsumeToken();
        ConsumeToken();
    }
}