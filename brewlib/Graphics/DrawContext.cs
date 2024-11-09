namespace BrewLib.Graphics;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public sealed class DrawContext : IDisposable
{
    List<IDisposable> disposables = [];
    Dictionary<Type, object> references = [];

    public T Get<T>() where T : class => Unsafe.As<T>(references[typeof(T)]);

    public void Register<T>(T obj, bool dispose = false) where T : class
    {
        references.Add(typeof(T), obj);
        if (dispose && obj is IDisposable disposable) disposables.Add(disposable);
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (disposed) return;

        foreach (var disposable in disposables) disposable.Dispose();
        disposables.Clear();
        disposables = null;

        references.Clear();
        references = null;

        GC.SuppressFinalize(this);
        disposed = true;
    }

    #endregion
}