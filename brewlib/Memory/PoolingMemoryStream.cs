namespace BrewLib.Memory;

using System;
using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.Memory;

public sealed class PoolingMemoryStream : Stream
{
    readonly MemoryAllocator _allocator;
    int length, position;
    Memory<byte> memory;
    IMemoryOwner<byte> memoryOwner;

    public PoolingMemoryStream(MemoryAllocator allocator = null)
    {
        _allocator = allocator ?? MemoryAllocator.Default;
        memoryOwner = _allocator.Allocate<byte>(0);
        memory = memoryOwner.Memory;
        length = 0;
        position = 0;
    }

    public ReadOnlySpan<byte> WrittenSpan => length == 0 ? default : memory.Span[..length];
    public ReadOnlyMemory<byte> WrittenMemory => length == 0 ? default : memory[..length];

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => length;

    public override long Position
    {
        get => position;
        set => position = value >= 0 ? (int)value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    void EnsureCapacity(long requiredLength)
    {
        if (requiredLength <= memory.Length) return;

        var newCapacity = Math.Max(256, memory.Length * 2);
        while (newCapacity < requiredLength) newCapacity *= 2;

        var newMemoryOwner = _allocator.Allocate<byte>(newCapacity);
        memory[..length].CopyTo(newMemoryOwner.Memory);
        memoryOwner.Dispose();
        memoryOwner = newMemoryOwner;
        memory = newMemoryOwner.Memory;
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(new(buffer, offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureCapacity(position + buffer.Length);

        buffer.CopyTo(memory.Span[position..]);

        position += buffer.Length;
        length = Math.Max(length, position);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if ((uint)offset + (uint)count > buffer.Length) throw new ArgumentOutOfRangeException();

        var bytesToRead = Math.Min(count, length - position);
        if (bytesToRead <= 0) return 0;

        memory.Span.Slice(position, bytesToRead).CopyTo(new(buffer, offset, count));
        position += bytesToRead;
        return bytesToRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            SeekOrigin.End => length + offset,
            _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
        };

        if (newPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Attempted to seek before start of stream");

        return position = (int)newPosition;
    }

    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        EnsureCapacity(value);
        length = (int)value;
        position = Math.Min(position, length);
    }

    public override void Flush() { }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            memoryOwner?.Dispose();
            memoryOwner = null;
            memory = default;
        }

        base.Dispose(disposing);
    }
}