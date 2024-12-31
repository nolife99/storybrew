namespace BrewLib.Memory;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Util;

public sealed class UnmanagedList<T> : MemoryManager<T>, IList<T>, IReadOnlyList<T>, IList where T : struct
{
    static readonly EqualityComparer<T> _comparer = EqualityComparer<T>.Default;
    Buffer buf;

    public UnmanagedList(int capacity = 0)
    {
        if (capacity > 0) buf = new(capacity);
    }

    public bool IsSynchronized => false;

    public bool IsFixedSize => false;

    public object SyncRoot => this;

    object IList.this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetReference(index);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => GetReference(index) = Unsafe.Unbox<T>(value);
    }

    public int Add(object value)
    {
        Add(ref Unsafe.Unbox<T>(value));
        return Count - 1;
    }

    public void Remove(object value) => Remove(ref Unsafe.Unbox<T>(value));

    public void CopyTo(Array array, int index) => CopyTo(Unsafe.As<Array, T[]>(ref array), index);

    public bool Contains(object value) => Contains(ref Unsafe.Unbox<T>(value));

    public int IndexOf(object value) => IndexOf(ref Unsafe.Unbox<T>(value));

    public void Insert(int index, object value) => Insert(index, ref Unsafe.Unbox<T>(value));

    public bool IsReadOnly => false;

    public int Count { get; private set; }

    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetReference(index);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => GetReference(index) = value;
    }

    public void Add(T item) => Add(ref item);

    public void Insert(int index, T item) => Insert(index, ref item);

    public int IndexOf(T item) => IndexOf(ref item);
    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count);
        if (index < --Count) buf.AsSpan(index + 1).CopyTo(buf.AsSpan(index));
    }
    public void Clear() => Count = 0;

    public bool Contains(T item) => Contains(ref item);

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (buf.Length == 0) return;
        buf.AsSpan(0, Count).CopyTo(array.AsSpan(arrayIndex));
    }

    public bool Remove(T item) => Remove(ref item);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    public EnumeratorRef GetEnumerator() => new(buf, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(ref T item)
    {
        if (buf.Length > Count) buf[Count++] = item;
        else
        {
            var length = Count;
            EnsureCapacity(length + 1);
            Count = length + 1;
            buf[length] = item;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(ref T item)
    {
        var index = IndexOf(ref item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int index, ref T item)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)Count);
        if (buf.Length == Count) EnsureCapacity(buf.Length + 1);

        if (index < Count) buf.AsSpan(index, Count - index).CopyTo(buf.AsSpan(index + 1));

        buf[index] = item;
        Count++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(ref T item)
    {
        for (var i = 0; i < Count; i++)
            if (_comparer.Equals(buf[i], item))
                return i;

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(ref T item)
    {
        for (var i = 0; i < Count; i++)
            if (_comparer.Equals(buf[i], item))
                return true;

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sort(IComparer<T> comparer = null) => buf.AsSpan(0, Count).Sort(comparer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sort(int index, int count, IComparer<T> comparer) => buf.AsSpan().Slice(index, count).Sort(comparer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetReference(int index) => ref buf.AsSpan(0, Count)[index];

    public void AddRange(ReadOnlySpan<T> items)
    {
        if (items.IsEmpty) return;

        if (SpanContainsMemory(buf.AsSpan(), ref MemoryMarshal.GetReference(items)))
        {
            if (EnsureCapacityWithoutDisposingOld(Count + items.Length, out var old))
            {
                old.Free();
                items.CopyTo(buf.AsSpan(Count));
            }
            else items.CopyTo(buf.AsSpan(Count));
        }
        else
        {
            EnsureCapacity(Count + items.Length);
            Unsafe.CopyBlock(ref Unsafe.As<T, byte>(ref buf[0]),
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(items)),
                (uint)(items.Length * Unsafe.SizeOf<T>()));
        }

        Count += items.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(IEnumerable<T> items) => InsertRange(Count, items);

    public void InsertRange(int index, ReadOnlySpan<T> items)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)Count);

        if (items.IsEmpty) return;

        ref var itemsHead = ref MemoryMarshal.GetReference(items);
        if (SpanContainsMemory(buf.AsSpan(), ref itemsHead))
        {
            if (EnsureCapacityWithoutCopy(Count + items.Length, out var old))
            {
                Unsafe.CopyBlock(ref Unsafe.As<T, byte>(ref buf[0]),
                    ref Unsafe.As<T, byte>(ref old[0]),
                    (uint)(index * Unsafe.SizeOf<T>()));

                Unsafe.CopyBlock(ref Unsafe.As<T, byte>(ref buf[index + items.Length]),
                    ref Unsafe.As<T, byte>(ref old[index]),
                    (uint)((Count - index) * Unsafe.SizeOf<T>()));

                old.Free();

                Unsafe.CopyBlock(ref Unsafe.As<T, byte>(ref buf[0]),
                    ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(items)),
                    (uint)(items.Length * Unsafe.SizeOf<T>()));
            }
            else
            {
                var head = buf.AsSpan(0, index);
                var tail = buf.AsSpan(index, Count - index);

                var headHasItemTail =
                    SpanContainsMemory(head, ref Unsafe.Add(ref MemoryMarshal.GetReference(items), items.Length - 1));

                switch (SpanContainsMemory(head, ref itemsHead))
                {
                    case true when headHasItemTail:
                        tail.CopyTo(buf.AsSpan(index + items.Length));
                        items.CopyTo(buf.AsSpan(index));
                        break;

                    case false when !headHasItemTail:
                    {
                        var insertOfs = Unsafe.ByteOffset(ref MemoryMarshal.GetReference(tail), ref itemsHead);
                        var tailMoved = buf.AsSpan(index + items.Length);
                        tail.CopyTo(tailMoved);

                        MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(tailMoved), insertOfs),
                                items.Length)
                            .CopyTo(buf.AsSpan(index));

                        break;
                    }
                    default:
                    {
                        var itemsHeadLen = (int)Unsafe.ByteOffset(ref itemsHead, ref MemoryMarshal.GetReference(tail));
                        var tailMoved = buf.AsSpan(index + items.Length, tail.Length);
                        tail.CopyTo(tailMoved);
                        var insertSpan = MemoryMarshal.Cast<T, byte>(buf.AsSpan(index));
                        MemoryMarshal.Cast<T, byte>(items)[..itemsHeadLen].CopyTo(insertSpan);

                        MemoryMarshal.Cast<T, byte>(tailMoved)[..(items.Length * Unsafe.SizeOf<T>() - itemsHeadLen)]
                            .CopyTo(insertSpan[itemsHeadLen..]);

                        break;
                    }
                }
            }
        }
        else
        {
            EnsureCapacity(Count + items.Length);
            if (index < Count) buf.AsSpan(index, Count - index).CopyTo(buf.AsSpan(index + items.Length));

            Unsafe.CopyBlock(ref Unsafe.As<T, byte>(ref buf[0]),
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(items)),
                (uint)(items.Length * Unsafe.SizeOf<T>()));
        }

        Count += items.Length;
    }

    public void InsertRange(int index, IEnumerable<T> items)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)Count);
        ArgumentNullException.ThrowIfNull(items);

        switch (items)
        {
            case UnmanagedList<T> ul: InsertRange(index, ul.buf.AsSpan(0, ul.Count)); break;
            case T[] a: InsertRange(index, a.AsSpan()); break;
            case List<T> l: InsertRange(index, CollectionsMarshal.AsSpan(l)); break;
            case ICollection<T> c:
            {
                var count = c.Count;
                if (count > 0)
                {
                    EnsureCapacity(Count + count);
                    if (index < Count) buf.AsSpan(index, Count - index).CopyTo(buf.AsSpan(index + count));

                    var rent = ArrayPool<T>.Shared.Rent(count);
                    try
                    {
                        c.CopyTo(rent, 0);
                        Unsafe.CopyBlock(ref Unsafe.As<T, byte>(ref buf[0]),
                            ref Unsafe.As<T, byte>(ref MemoryMarshal.GetArrayDataReference(rent)),
                            (uint)(count * Unsafe.SizeOf<T>()));
                    }
                    finally
                    {
                        ArrayPool<T>.Shared.Return(rent);
                    }

                    Count += count;
                }

                break;
            }
            default:
            {
                foreach (var item in items) Insert(index++, item);
                break;
            }
        }
    }

    ~UnmanagedList() => Dispose(false);

    protected override void Dispose(bool disposing)
    {
        if (buf.Ptr == 0) return;
        buf.Free();
        buf = default;
        Count = 0;
    }

    public override Span<T> GetSpan() => buf.AsSpan(0, Count);

    public override unsafe MemoryHandle Pin(int elementIndex = 0) => new((void*)buf.Ptr);

    public override void Unpin() { }

    void EnsureCapacity(int min)
    {
        if (EnsureCapacityWithoutDisposingOld(min, out var old)) old.Free();
    }

    bool EnsureCapacityWithoutDisposingOld(int min, out Buffer old)
    {
        if (!EnsureCapacityWithoutCopy(min, out old)) return false;
        if (Count > 0)
            Unsafe.CopyBlock(ref Unsafe.As<T, byte>(ref buf[0]),
                ref Unsafe.As<T, byte>(ref old[0]),
                (uint)(Count * Unsafe.SizeOf<T>()));

        return true;
    }

    bool EnsureCapacityWithoutCopy(int min, out Buffer old)
    {
        if (buf.Length < min)
        {
            Buffer newBuf = new((int)BitOperations.RoundUpToPowerOf2((uint)min));
            old = buf;
            buf = newBuf;
            return true;
        }

        old = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool SpanContainsMemory(Span<T> span, ref T target) => span.Length > 0 &&
        !Unsafe.IsAddressLessThan(ref target, ref MemoryMarshal.GetReference(span)) &&
        !Unsafe.IsAddressGreaterThan(ref target, ref Unsafe.Add(ref MemoryMarshal.GetReference(span), span.Length - 1));

    class Enumerator : IEnumerator<T>
    {
        readonly UnmanagedList<T> _list;
        int _index;

        internal Enumerator(UnmanagedList<T> list)
        {
            _list = list;
            _index = 0;
            Current = default;
        }

        public T Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose() { }

        public bool MoveNext()
        {
            if (_index >= _list.Count) return false;
            Current = _list[_index];
            _index++;
            return true;
        }

        public void Reset()
        {
            _index = 0;
            Current = default;
        }
    }

    internal readonly record struct Buffer(int Length)
    {
        public readonly nint Ptr = Native.AllocateMemory(Length * Unsafe.SizeOf<T>());

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.Add(ref Unsafe.AddByteOffset(ref Unsafe.NullRef<T>(), Ptr), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free() => Native.FreeMemory(Ptr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan() => MemoryMarshal.CreateSpan(ref this[0], Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan(int start) => MemoryMarshal.CreateSpan(ref this[start], Length - start);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan(int start, int length) => MemoryMarshal.CreateSpan(ref this[start], length);
    }

    public ref struct EnumeratorRef
    {
        readonly Buffer _span;
        readonly int _size;
        int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal EnumeratorRef(Buffer span, int count)
        {
            _span = span;
            _size = count;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            var index = _index + 1;
            if (index >= _size) return false;
            _index = index;
            return true;
        }

        public ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _span[_index];
        }
    }
}