namespace BrewLib.Util;

using System;

public readonly record struct ActionDisposable(Action action) : IDisposable
{
    public void Dispose() => action?.Invoke();
}