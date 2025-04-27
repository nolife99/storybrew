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
    public static bool IsFullyTransparent(this Image<Rgba32> source)
    {
        var buffer = source.Frames.RootFrame.PixelBuffer;
        return buffer.MemoryGroup.Count == 1 ?
            IsFullyTransparentContiguous(MemoryMarshal.CreateReadOnlySpan(
                ref MemoryMarshal.GetReference(buffer.DangerousGetRowSpan(0)),
                buffer.Width * buffer.Height)) :
            IsFullyTransparentDiscontiguous(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsFullyTransparentContiguous(ReadOnlySpan<Rgba32> buffer)
    {
        if (Vector.IsHardwareAccelerated)
        {
            var zeroVector = Vector<int>.Zero;
            var vectorSize = (nuint)Vector<int>.Count;
            var len = (nuint)buffer.Length;

            Vector<int> alphaMask = new(Unsafe.BitCast<Rgba32, int>(new(0, 0, 0, 255)));

            ref var first = ref Unsafe.As<Rgba32, int>(ref MemoryMarshal.GetReference(buffer));

            nuint offset = 0;
            while (offset + vectorSize <= len)
            {
                if ((Vector.LoadUnsafe(ref first, offset) & alphaMask) >> 24 != zeroVector) return false;

                offset += vectorSize;
            }

            if (len - offset == 0) return true;
            if ((Vector.LoadUnsafe(ref first, len - vectorSize) & alphaMask) >> 24 != zeroVector) return false;
        }
        else
            foreach (ref readonly var pixel in buffer)
                if (pixel.A != 0)
                    return false;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsFullyTransparentDiscontiguous(Buffer2D<Rgba32> buffer)
    {
        for (var y = 0; y < buffer.Height; ++y)
            if (!IsFullyTransparentContiguous(buffer.DangerousGetRowSpan(y)))
                return false;

        return true;
    }

    public static Rectangle FindTransparencyBounds(Image<Rgba32> source)
    {
        int xMin = source.Width, yMin = source.Height, xMax = -1, yMax = -1;

        var buffer = source.Frames.RootFrame.PixelBuffer;
        for (var y = 0; y < source.Height; ++y)
        {
            var srcData = buffer.DangerousGetRowSpan(y);
            ref var rowRefAsColor = ref MemoryMarshal.GetReference(srcData);

            for (var x = 0; x < srcData.Length; ++x)
                if (Unsafe.Add(ref rowRefAsColor, x).A != 0)
                {
                    if (x < xMin) xMin = x;
                    if (x > xMax) xMax = x;
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;
                }
        }

        return xMin <= xMax && yMin <= yMax ? Rectangle.FromLTRB(xMin, yMin, xMax + 1, yMax + 1) : default;
    }
}