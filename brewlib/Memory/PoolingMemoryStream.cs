namespace BrewLib.Memory;

using System;
using System.Buffers;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

public sealed class PoolingMemoryStream : MemoryStream
{
    static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    static readonly Action<MemoryStream, byte[]> set__buffer;
    static readonly Action<MemoryStream, int> set__capacity;
    bool _disposed;

    static PoolingMemoryStream()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

        // TODO: Change to use UnsafeAccessorAttribute

        var target = Expression.Parameter(typeof(MemoryStream));
        var param = Expression.Parameter(typeof(byte[]));

        set__buffer = Expression.Lambda<Action<MemoryStream, byte[]>>(
                Expression.Assign(Expression.Field(target, target.Type.GetField("_buffer", flags)), param),
                true,
                target,
                param)
            .Compile();

        param = Expression.Parameter(typeof(int));

        set__capacity = Expression.Lambda<Action<MemoryStream, int>>(
                Expression.Assign(Expression.Field(target, target.Type.GetField("_capacity", flags)), param),
                true,
                target,
                param)
            .Compile();
    }

    public PoolingMemoryStream() : this(0) { }

    public PoolingMemoryStream(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be non-negative");

        InitializeBuffer(capacity);
    }

    public override int Capacity
    {
        get => base.Capacity;
        set
        {
            if (value < Length)
                throw new ArgumentOutOfRangeException(nameof(value), "Capacity cannot be less than stream length");

            if (value == Capacity) return;

            var oldBuffer = GetBuffer();
            var newBuffer = Pool.Rent(value);

            Buffer.BlockCopy(oldBuffer, 0, newBuffer, 0, Math.Min((int)Length, value));

            set__buffer(this, newBuffer);
            set__capacity(this, value);

            Pool.Return(oldBuffer);
        }
    }

    void InitializeBuffer(int capacity)
    {
        var buffer = Pool.Rent(capacity);
        set__buffer(this, buffer);
        set__capacity(this, buffer.Length);
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;

        try
        {
            if (disposing)
            {
                var buffer = GetBuffer();
                Pool.Return(buffer);

                set__buffer(this, null);
                set__capacity(this, 0);
            }
        }
        finally
        {
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}