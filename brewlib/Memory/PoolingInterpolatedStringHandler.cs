namespace BrewLib.Memory;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Tiny.PooledCollections.Generic.Temporary;

[InterpolatedStringHandler] public ref struct PoolingInterpolatedStringHandler
{
    const int MaxBufferSize = 512;
    const char whitespace = ' ';

    readonly IFormatProvider provider;
    internal TempList<char> buffer;

    public PoolingInterpolatedStringHandler(int literalLength, int formattedCount, IFormatProvider provider = null)
    {
        const int charsPerPlaceholder = 10;
        var length = charsPerPlaceholder * formattedCount + literalLength;

        buffer = (uint)length <= (uint)Array.MaxLength ?
            TempList.Create<char>(length) :
            throw new InsufficientMemoryException();

        this.provider = provider;
    }

    Span<char> GetSpan(int sizeHint) => buffer.GetInsertSpan(buffer.Count, sizeHint);

    public void AppendLiteral(string value) => AppendFormatted(value.AsSpan());

    [MethodImpl(MethodImplOptions.AggressiveOptimization)] // Avoids boxing value types by skipping tiered compilation
    public void AppendFormatted<T>(T value, string format = null)
    {
        switch (value)
        {
            case ISpanFormattable:
                Span<char> span = stackalloc char[MaxBufferSize];
                if (((ISpanFormattable)value).TryFormat(span, out var charsWritten, format, provider))
                    span[..charsWritten].CopyTo(GetSpan(charsWritten));

                break;

            case IFormattable: AppendLiteral(((IFormattable)value).ToString(format, provider)); break;

            case not null: AppendLiteral(value.ToString()); break;
        }
    }

    void AppendFormatted(ReadOnlySpan<char> value, int alignment, bool leftAlign)
    {
        Debug.Assert(alignment >= 0);

        var padding = alignment - value.Length;
        if (padding <= 0)
        {
            AppendFormatted(value);
            return;
        }

        var span = GetSpan(alignment);
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

    public void AppendFormatted(ReadOnlySpan<char> value, int alignment)
    {
        var leftAlign = false;

        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        AppendFormatted(value, alignment, leftAlign);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)] // Avoids boxing value types by skipping tiered compilation
    public void AppendFormatted<T>(T value, int alignment, string format = null)
    {
        var leftAlign = false;

        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        switch (value)
        {
            case ISpanFormattable:
                Span<char> span = stackalloc char[MaxBufferSize];
                if (!((ISpanFormattable)value).TryFormat(span, out var charsWritten, format, provider)) break;

                var padding = alignment - charsWritten;
                var dest = GetSpan(charsWritten + padding);

                if (padding <= 0) { }
                else if (leftAlign)
                {
                    span[..charsWritten].CopyTo(dest);
                    dest[charsWritten..].Fill(whitespace);
                }
                else
                {
                    dest[..padding].Fill(whitespace);
                    span[..charsWritten].CopyTo(dest[padding..]);
                }

                break;

            case IFormattable:
                AppendFormatted(((IFormattable)value).ToString(format, provider).AsSpan(), alignment, leftAlign); break;

            case not null: AppendFormatted(value.ToString().AsSpan(), alignment, leftAlign); break;
        }
    }

    public void AppendFormatted(ReadOnlySpan<char> value) => value.CopyTo(GetSpan(value.Length));
}