namespace BrewLib.Graphics;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using Tiny.PooledCollections.Generic;

public sealed class DrawContext : IDisposable
{
    readonly PooledList<IDisposable> disposables = new();
    FrozenDictionary<Type, object> frozenReferences;
    Dictionary<Type, object> references = new();

    public DrawContext()
    {
        // Prevent LOH allocations
        var allocator = MemoryAllocator.Default;
        allocator.GetType()
            .GetField("sharedArrayPoolThresholdInBytes", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(allocator, 65536);
    }

    public T Get<T>() where T : class => Unsafe.As<T>(frozenReferences.GetValueRefOrNullRef(typeof(T)));

    public void Register<T>(T obj, bool dispose = false) where T : class
    {
        if (references is null) throw new InvalidOperationException("Can't register to frozen DrawContext");

        references[typeof(T)] = obj;
        if (dispose && obj is IDisposable disposable) disposables.Add(disposable);
    }

    public void Freeze()
    {
        frozenReferences = references.ToFrozenDictionary();
        references = null;
    }

    #region IDisposable Support

    bool disposed;

    public void Dispose()
    {
        if (disposed) return;

        foreach (var disposable in disposables) disposable.Dispose();
        disposables.Dispose();

        disposed = true;
    }

    #endregion
}