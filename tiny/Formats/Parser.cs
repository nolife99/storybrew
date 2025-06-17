namespace Tiny.Formats;

using System;

public abstract class Parser<TToken>(Action<TinyToken> callback, int virtualIndent)
{
    protected Action<TinyToken> Callback => callback;
    protected int VirtualIndent => virtualIndent;

    public abstract void Parse(ParseContext<TToken> context);
    public abstract void End();
}