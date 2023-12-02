using System;
using System.Collections.Generic;

namespace BrewLib.Graphics;

public sealed class DrawContext : IDisposable
{
    Dictionary<Type, object> references = [];
    List<IDisposable> disposables = [];

    public T Get<T>() => (T)references[typeof(T)];

    public void Register<T>(T obj, bool dispose = false)
    {
        references.Add(typeof(T), obj);
        if (dispose && obj is IDisposable disposable) disposables.Add(disposable);
    }

    #region IDisposable Support

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            for (int i = 0; i < disposables.Count; i++) disposables[i].Dispose();
            references = null;
            disposables = null;
            disposed = true;
        }
    }

    #endregion
}