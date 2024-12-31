namespace BrewLib.IO;

using System;
using System.IO;

public class ByteCounterStream : Stream
{
    long length;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => length;

    public override long Position { get => length; set => throw new NotSupportedException(); }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => length = value;
    public override void Write(byte[] buffer, int offset, int count) => length += count;
    public override void Write(ReadOnlySpan<byte> buffer) => length += buffer.Length;
}