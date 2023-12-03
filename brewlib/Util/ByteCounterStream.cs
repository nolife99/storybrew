using System;
using System.IO;

namespace BrewLib.Util;

public class ByteCounterStream : Stream
{
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    long length;
    public override long Length => length;
    public override long Position
    {
        get => length;
        set { }
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => 0;
    public override long Seek(long offset, SeekOrigin origin) => 0;

    public override void SetLength(long value) => length = value;
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (offset + count > buffer.Length) throw new OverflowException("Sum of offset and count is greater than buffer size");
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0) throw new ArgumentOutOfRangeException("offset/count should be greater than zero");

        length += count;
    }
}