namespace BrewLib.Memory;

using System;
using System.Runtime.CompilerServices;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

[InterpolatedStringHandler] public ref struct PoolingInterpolatedStringHandler
{
    readonly IFormatProvider provider;
    internal TempList<char> buffer;

    public PoolingInterpolatedStringHandler(int literalLength, int formattedCount, IFormatProvider provider = null)
    {
        var length = 10 * formattedCount + literalLength;

        buffer = (uint)length <= (uint)Array.MaxLength ?
            TempList.Create<char>(length) :
            throw new InsufficientMemoryException();

        this.provider = provider;
    }

    public void AppendLiteral(string value) => AppendFormatted(value.AsSpan());

    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string format = null)
    {
        var leftAlign = false;
        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        var padding = alignment - value.Length;
        if (padding <= 0)
        {
            buffer.AddRange(value);
            return;
        }

        var span = buffer.GetInsertSpan(buffer.Count, alignment, false);

        const char whitespace = ' ';
        if (leftAlign)
        {
            span.Slice(value.Length, padding).Fill(whitespace);
            value.CopyTo(span);
        }
        else
        {
            span[..padding].Fill(whitespace);
            value.CopyTo(span[padding..]);
        }
    }

    public void AppendFormatted<T>(T value, int alignment = 0, string format = null)
    {
        switch (value)
        {
            case ISpanFormattable:
                buffer.GetUnsafe(out var array, out var count);
                var bufferSize = array.Length - count;

                int charsWritten;
                if (value is Enum)
                    while (!TryFormatUnconstrained(null, value, new(array, count, bufferSize), out charsWritten, format))
                        Grow(ref buffer);
                else
                    while (!((ISpanFormattable)value).TryFormat(new(array, count, bufferSize),
                        out charsWritten,
                        format,
                        provider))
                        Grow(ref buffer);

                buffer.GetInsertSpan(count, charsWritten, false);
                break;

                void Grow(scoped ref TempList<char> buf)
                {
                    bufferSize = array.Length < Array.MaxLength ? bufferSize << 1 : throw new InsufficientMemoryException();

                    buf.EnsureCapacity(buf.Count + bufferSize);
                    buf.GetUnsafe(out array, out _);
                }

            case IFormattable: AppendFormatted(((IFormattable)value).ToString(format, provider).AsSpan(), alignment); break;

            case not null: AppendFormatted(value.ToString().AsSpan(), alignment); break;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "TryFormatUnconstrained")]
    static extern bool TryFormatUnconstrained<TEnum>(Enum c,
        TEnum value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default);
}