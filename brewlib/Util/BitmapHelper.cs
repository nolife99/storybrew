namespace BrewLib.Util;

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

public static class BitmapHelper
{
    const int AlphaMask = unchecked((int)0xFF000000);

    public static bool IsFullyTransparent(this Image<Rgba32> source)
    {
        var buffer = source.Frames.RootFrame.PixelBuffer;
        return buffer.MemoryGroup.Count == 1
            ? isFullyTransparentContiguous(MemoryMarshal.CreateReadOnlySpan(
                ref MemoryMarshal.GetReference(buffer.DangerousGetRowSpan(0)),
                buffer.Width * buffer.Height))
            : isFullyTransparentDiscontiguous(buffer);
    }

    public static bool IsFullyOpaque(this Image<Rgba32> source)
    {
        var buffer = source.Frames.RootFrame.PixelBuffer;
        return buffer.MemoryGroup.Count == 1
            ? isFullyOpaqueContiguous(MemoryMarshal.CreateReadOnlySpan(
                ref MemoryMarshal.GetReference(buffer.DangerousGetRowSpan(0)),
                buffer.Width * buffer.Height))
            : isFullyOpaqueDiscontiguous(buffer);
    }

    public static Rectangle FindTransparencyBounds(Image<Rgba32> source)
    {
        var buffer = source.Frames.RootFrame.PixelBuffer;
        var width = source.Width;
        var height = source.Height;
        if (width == 0 || height == 0) return Rectangle.Empty;

        var yMin = -1;
        var xMin = width;
        var xMax = -1;
        for (var y = 0; y < height; ++y)
        {
            var row = buffer.DangerousGetRowSpan(y);
            var first = findFirstOpaque(row);
            if (first < 0) continue;

            yMin = y;
            xMin = first;
            
            var lastInRange = findLastOpaque(row[(first + 1)..]);
            xMax = lastInRange < 0 ? first : first + 1 + lastInRange;
            break;
        }
        if (yMin < 0) return Rectangle.Empty;

        var yMax = yMin;
        for (var y = height - 1; y > yMin; --y)
        {
            var row = buffer.DangerousGetRowSpan(y);
            var last = findLastOpaque(row);
            if (last < 0) continue;

            yMax = y;
            if (last > xMax) xMax = last;
            if (last < xMin) xMin = last;
            
            var firstInRange = findFirstOpaque(row[..last]);
            if (firstInRange >= 0 && firstInRange < xMin) xMin = firstInRange;
            break;
        }

        for (var y = yMin + 1; y < yMax; ++y)
        {
            var row = buffer.DangerousGetRowSpan(y);
            if (xMin > 0)
            {
                var first = findFirstOpaque(row[..xMin]);
                if (first >= 0) xMin = first;
            }

            if (xMax < width - 1)
            {
                var rightStart = xMax + 1;
                var lastInRange = findLastOpaque(row[rightStart..]);
                if (lastInRange >= 0) xMax = rightStart + lastInRange;
            }

            if (xMin == 0 && xMax == width - 1) break;
        }

        return xMin <= xMax ? Rectangle.FromLTRB(xMin, yMin, xMax + 1, yMax + 1) : Rectangle.Empty;
    }

    static bool isFullyTransparentContiguous(scoped ReadOnlySpan<Rgba32> buffer)
    {
        var len = buffer.Length;
        if (len == 0) return true;

        if (Vector.IsHardwareAccelerated && len >= Vector<int>.Count)
        {
            var vectorSize = Vector<int>.Count;
            ref var first = ref Unsafe.As<Rgba32, int>(ref MemoryMarshal.GetReference(buffer));
            var zero = Vector<int>.Zero;
            var alphaMask = new Vector<int>(AlphaMask);

            int offset = 0;
            while (offset + vectorSize <= len)
            {
                if (!Vector.EqualsAll(Vector.LoadUnsafe(ref first, (nuint)offset) & alphaMask, zero))
                    return false;
                offset += vectorSize;
            }

            if (offset < len)
            {
                if (!Vector.EqualsAll(Vector.LoadUnsafe(ref first, (nuint)(len - vectorSize)) & alphaMask, zero))
                    return false;
            }
            return true;
        }

        foreach (ref readonly var pixel in buffer)
            if (pixel.A != 0)
                return false;
        return true;
    }

    static bool isFullyTransparentDiscontiguous(Buffer2D<Rgba32> buffer)
    {
        for (var y = 0; y < buffer.Height; ++y)
            if (!isFullyTransparentContiguous(buffer.DangerousGetRowSpan(y)))
                return false;
        return true;
    }

    static bool isFullyOpaqueContiguous(scoped ReadOnlySpan<Rgba32> buffer)
    {
        var len = buffer.Length;
        if (len == 0) return true;

        if (Vector.IsHardwareAccelerated && len >= Vector<int>.Count)
        {
            var vectorSize = Vector<int>.Count;
            ref var first = ref Unsafe.As<Rgba32, int>(ref MemoryMarshal.GetReference(buffer));
            var alphaMask = new Vector<int>(AlphaMask);

            int offset = 0;
            while (offset + vectorSize <= len)
            {
                if (!Vector.EqualsAll(Vector.LoadUnsafe(ref first, (nuint)offset) & alphaMask, alphaMask))
                    return false;
                offset += vectorSize;
            }

            if (offset < len)
            {
                if (!Vector.EqualsAll(Vector.LoadUnsafe(ref first, (nuint)(len - vectorSize)) & alphaMask, alphaMask))
                    return false;
            }
            return true;
        }

        foreach (ref readonly var pixel in buffer)
            if (pixel.A != 255)
                return false;
        return true;
    }

    static bool isFullyOpaqueDiscontiguous(Buffer2D<Rgba32> buffer)
    {
        for (var y = 0; y < buffer.Height; ++y)
            if (!isFullyOpaqueContiguous(buffer.DangerousGetRowSpan(y)))
                return false;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int findFirstOpaque(scoped ReadOnlySpan<Rgba32> span)
    {
        var len = span.Length;
        if (len == 0) return -1;

        if (Vector.IsHardwareAccelerated && len >= Vector<int>.Count)
        {
            var vectorSize = Vector<int>.Count;
            ref var first = ref Unsafe.As<Rgba32, int>(ref MemoryMarshal.GetReference(span));
            var zero = Vector<int>.Zero;
            var alphaMask = new Vector<int>(AlphaMask);

            int offset = 0;
            while (offset + vectorSize <= len)
            {
                if (!Vector.EqualsAll(Vector.LoadUnsafe(ref first, (nuint)offset) & alphaMask, zero))
                    return offset + findFirstOpaqueScalar(span.Slice(offset, vectorSize));
                offset += vectorSize;
            }

            if (offset < len)
            {
                var tail = findFirstOpaqueScalar(span[offset..]);
                if (tail >= 0) return offset + tail;
            }
            return -1;
        }

        return findFirstOpaqueScalar(span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int findLastOpaque(scoped ReadOnlySpan<Rgba32> span)
    {
        var len = span.Length;
        if (len == 0) return -1;

        if (Vector.IsHardwareAccelerated && len >= Vector<int>.Count)
        {
            var vectorSize = Vector<int>.Count;
            ref var first = ref Unsafe.As<Rgba32, int>(ref MemoryMarshal.GetReference(span));
            var zero = Vector<int>.Zero;
            var alphaMask = new Vector<int>(AlphaMask);

            var offset = len - vectorSize;
            while (offset >= 0)
            {
                if (!Vector.EqualsAll(Vector.LoadUnsafe(ref first, (nuint)offset) & alphaMask, zero))
                    return offset + findLastOpaqueScalar(span.Slice(offset, vectorSize));
                offset -= vectorSize;
            }

            var headLen = len % vectorSize;
            if (headLen > 0)
                return findLastOpaqueScalar(span[..headLen]);
            return -1;
        }

        return findLastOpaqueScalar(span);
    }

    static int findFirstOpaqueScalar(scoped ReadOnlySpan<Rgba32> span)
    {
        for (var i = 0; i < span.Length; ++i)
            if (span[i].A != 0) return i;
        return -1;
    }

    static int findLastOpaqueScalar(scoped ReadOnlySpan<Rgba32> span)
    {
        for (var i = span.Length - 1; i >= 0; --i)
            if (span[i].A != 0) return i;
        return -1;
    }
}
