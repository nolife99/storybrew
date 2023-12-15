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

    public override int Read(byte[] buffer, int offset, int count) => int.MinValue;
    public override long Seek(long offset, SeekOrigin origin) => int.MinValue;

    public override void SetLength(long value) => length = value;
    public override void Write(byte[] buffer, int offset, int count) => length += count;
}