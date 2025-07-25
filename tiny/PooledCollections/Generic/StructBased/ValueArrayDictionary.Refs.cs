namespace Tiny.PooledCollections.Generic.StructBased;

using System;
using System.Runtime.CompilerServices;

partial struct ValueArrayDictionary<TKey, TValue>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd(TKey key)
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        _values[findIndex] = default;

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd(TKey key, Func<TValue> builder)
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        _values[findIndex] = builder();

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetOrAdd<W>(TKey key, FuncRef<W, TValue> builder, ref W parameter)
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        _values[findIndex] = builder(ref parameter);

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue RecycleOrAdd<TValueProxy>(TKey key, Func<TValueProxy> builder, ActionRef<TValueProxy> recycler)
        where TValueProxy : class, TValue
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        if (_values[findIndex] is null) _values[findIndex] = builder();
        else recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[findIndex]));

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue RecycleOrAdd<TValueProxy, U>(TKey key,
        FuncRef<U, TValue> builder,
        ActionRef<TValueProxy, U> recycler,
        ref U parameter) where TValueProxy : class, TValue
    {
        if (TryFindIndex(key, out var findIndex)) return ref _values[findIndex];

        TryGetIndex(key, out findIndex);

        if (_values[findIndex] is null) _values[findIndex] = builder(ref parameter);
        else recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[findIndex]), ref parameter);

        return ref _values[findIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    //WARNING this method must stay stateless (not relying on states that can change, it's ok to read
    //constant states) because it will be used in multi-threaded parallel code
    public ref TValue GetIndexedValueByRef(int index) => ref _values[index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref TValue GetValueByRef(TKey key)
    {
#if DEBUG
        if (TryFindIndex(key, out var findIndex) == true) return ref _values[findIndex];

        ThrowHelper.ThrowKeyNotFoundException(key);
        return ref Unsafe.NullRef<TValue>();
#else

        //Burst is not able to vectorise code if throw is found, regardless if it's actually ever thrown
        TryFindIndex(key, out var findIndex);

        return ref _values[findIndex];
#endif
    }
}