namespace Tiny.Formats;

using System;

public abstract class Parser<TokenType>(Action<TinyToken> callback, int virtualIndent)
{
    protected readonly Action<TinyToken> Callback = callback;
    protected readonly int VirtualIndent = virtualIndent;

    public abstract void Parse(ParseContext<TokenType> context);
    public abstract void End();
}