namespace BrewLib.IO;

using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Util;

public sealed class SafeUnmanagedMemoryStream : Stream
{
    int capacity, length, position;
    nint currentBuffer;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;

    public override long Length => length;
    public override long Position { get => position; set => position = (int)value; }

    public override void Flush() { }

    void ReallocateBuffer(int minimumRequired)
        => currentBuffer = Native.ReallocateMemory(currentBuffer, capacity = minimumRequired);

    public override void SetLength(long value)
    {
        length = (int)value;
        if (currentBuffer == 0 || capacity < value) ReallocateBuffer((int)BitOperations.RoundUpToPowerOf2((uint)value));

        if (position < value) return;

        if (value == 0) position = 0;
        else position = length - 1;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (currentBuffer != 0)
        {
            Native.FreeMemory(currentBuffer);
            currentBuffer = 0;
        }

        length = 0;
        position = 0;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var oldValue = position;
        switch (origin)
        {
            case SeekOrigin.Begin: position = (int)offset; break;
            case SeekOrigin.End: position = length - (int)offset; break;
            case SeekOrigin.Current: position += (int)offset; break;
        }

        if (position >= 0 && position <= length) return position;

        position = oldValue;
        throw new ArgumentOutOfRangeException(nameof(offset), "Negative position");
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var count = buffer.Length;

        var readlen = count > length - position ? length - position : count;
        if (readlen <= 0) return 0;

        Unsafe.CopyBlock(
            ref MemoryMarshal.GetReference(buffer),
            ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), currentBuffer + position),
            (uint)readlen);

        position += readlen;
        return readlen;
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var count = buffer.Length;

        var endOffset = position + count;
        if (currentBuffer == 0 || endOffset > capacity)
            ReallocateBuffer((int)BitOperations.RoundUpToPowerOf2((uint)endOffset));

        Unsafe.CopyBlock(
            ref Unsafe.AddByteOffset(ref Unsafe.NullRef<byte>(), currentBuffer + position),
            ref MemoryMarshal.GetReference(buffer),
            (uint)count);

        if (endOffset > length) length = endOffset;
        position = endOffset;
    }
}