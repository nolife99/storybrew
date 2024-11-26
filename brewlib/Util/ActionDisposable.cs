namespace BrewLib.Util;

using System;

public sealed record ActionDisposable(Action action) : IDisposable
{
    public void Dispose() => action();
}