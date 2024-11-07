﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BrewLib.Graphics;

public sealed class DrawContext : IDisposable
{
    Dictionary<Type, object> references = [];
    List<IDisposable> disposables = [];

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
        if (!disposed)
        {
            foreach (var disposable in disposables) disposable.Dispose();
            references = null;
            disposables = null;

            GC.SuppressFinalize(this);
            disposed = true;
        }
    }

    #endregion
}