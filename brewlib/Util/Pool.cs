namespace BrewLib.Util;

using System;
using System.Buffers;
using System.Threading;

public sealed class Pool<T>(Action<T> disposer = null, bool singleThreaded = false) : IDisposable where T : class, new()
{
    readonly Lock _lock = new();

    T[] _array = [];
    int _head, _size, _tail;

    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(_array);
        _array = [];
    }

    public T Retrieve()
    {
        if (singleThreaded) return TryDequeue(out var obj) ? obj : new();
        lock (_lock) return TryDequeue(out var obj) ? obj : new();
    }

    public void Release(T obj)
    {
        disposer?.Invoke(obj);
        if (singleThreaded)
        {
            Enqueue(obj);
            return;
        }

        lock (_lock) Enqueue(obj);
    }

    void Enqueue(T item)
    {
        if (_size == _array.Length) Grow(_size + 1);

        _array[_tail] = item;
        MoveNext(ref _tail);
        _size++;
    }

    bool TryDequeue(out T result)
    {
        var head = _head;
        var array = _array;

        if (_size == 0)
        {
            result = null;
            return false;
        }

        result = array[head];
        array[head] = null;
        MoveNext(ref _head);
        _size--;
        return true;
    }

    void MoveNext(ref int index)
    {
        var tmp = index + 1;
        if (tmp == _array.Length) tmp = 0;
        index = tmp;
    }

    void Grow(int capacity)
    {
        var newcapacity = Math.Max(1.5f * _array.Length, _array.Length + 4);
        if (newcapacity < capacity) newcapacity = capacity;

        SetCapacity((int)newcapacity);
    }
    void SetCapacity(int capacity)
    {
        var newarray = ArrayPool<T>.Shared.Rent(capacity);
        if (_size > 0)
        {
            if (_head < _tail) _array.AsSpan(_head, _size).CopyTo(newarray);
            else
            {
                _array.AsSpan(_head, _array.Length - _head).CopyTo(newarray);
                _array.AsSpan(0, _tail).CopyTo(newarray.AsSpan(_array.Length - _head));
            }

            ArrayPool<T>.Shared.Return(_array);
        }

        _array = newarray;
        _head = 0;
        _tail = _size == capacity ? 0 : _size;
    }
}