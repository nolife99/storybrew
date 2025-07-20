namespace BrewLib.IO;

using System;
using System.IO;
using System.Text;

public class ByteCountingTextWriter(Encoding encoding) : TextWriter
{
    readonly Encoding _encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));

    public override Encoding Encoding => _encoding;

    public long ByteCount { get; private set; }

    public override void Write(char value) => ByteCount += _encoding.GetByteCount([value]);

    public override void Write(char[] buffer, int index, int count)
        => ByteCount += _encoding.GetByteCount(buffer, index, count);

    public override void Write(string value) => Write(value.AsSpan());

    public override void Write(ReadOnlySpan<char> buffer)
    {
        if (buffer.IsEmpty) return;

        ByteCount += _encoding.GetByteCount(buffer);
    }
}