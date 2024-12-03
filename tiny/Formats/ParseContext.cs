namespace Tiny.Formats;

using System;
using System.Collections.Generic;

public class ParseContext<TokenType> : IDisposable
{
    readonly Stack<Parser<TokenType>> parserStack = new();
    readonly IEnumerator<Token<TokenType>> tokenEnumerator;

    public Token<TokenType> CurrentToken, LookaheadToken;

    public ParseContext(IEnumerable<Token<TokenType>> tokens, Parser<TokenType> initialParser)
    {
        tokenEnumerator = tokens.GetEnumerator();
        initializeCurrentAndLookahead();

        parserStack.Push(initialParser);
    }

    public Parser<TokenType> Parser => parserStack.Count > 0 ? parserStack.Peek() : null;

    public int IndentLevel { get; private set; }

    public void Dispose()
    {
        while (Parser is not null)
        {
            Parser.End();
            PopParser();
        }

        tokenEnumerator.Dispose();
    }

    public void PopParser() => parserStack.Pop();
    public void PushParser(Parser<TokenType> parser) => parserStack.Push(parser);

    public void ReplaceParser(Parser<TokenType> parser)
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