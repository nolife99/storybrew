namespace BrewLib.Util;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public static class BitmapHelper
{
    public static bool IsFullyTransparent(Image<Rgba32> source)
    {
        var buffer = source.Frames.RootFrame.PixelBuffer;
        if (source.DangerousTryGetSinglePixelMemory(out var contiguousMem))
        {
            var contiguousSpan = contiguousMem.Span;
            if (Vector.IsHardwareAccelerated)
            {
                var zeroVector = Vector<int>.Zero;
                var vectorSize = Vector<int>.Count;

                Vector<int> alphaMask = new(Unsafe.BitCast<Rgba32, int>(new(0, 0, 0, 255)));

                ref var first = ref Unsafe.As<Rgba32, int>(ref MemoryMarshal.GetReference(contiguousSpan));

                var offset = 0;
                while (offset + vectorSize <= contiguousSpan.Length)
                {
                    if ((Vector.LoadUnsafe(ref first, (nuint)offset) & alphaMask) >> 24 != zeroVector) return false;

                    offset += vectorSize;
                }

                for (; offset < contiguousSpan.Length; ++offset)
                    if (Unsafe.As<int, Rgba32>(ref Unsafe.Add(ref first, offset)).A != 0)
                        return false;
            }
            else
                foreach (ref var pixel in contiguousSpan)
                    if (pixel.A != 0)
                        return false;

            return true;
        }

        if (Vector.IsHardwareAccelerated)
        {
            var width = source.Width;

            var zeroVector = Vector<int>.Zero;
            var vectorSize = Vector<int>.Count;

            Vector<int> alphaMask = new(Unsafe.BitCast<Rgba32, int>(new(0, 0, 0, 255)));
            for (var y = 0; y < source.Height; ++y)
            {
                var rowSpan = buffer.DangerousGetRowSpan(y);
                ref var rowFirst = ref Unsafe.As<Rgba32, int>(ref MemoryMarshal.GetReference(rowSpan));

                var x = 0;
                while (x + vectorSize <= width)
                {
                    if ((Vector.LoadUnsafe(ref rowFirst, (nuint)x) & alphaMask) >> 24 != zeroVector) return false;

                    x += vectorSize;
                }

                for (; x < width; ++x)
                    if (Unsafe.As<int, Rgba32>(ref Unsafe.Add(ref rowFirst, x)).A != 0)
                        return false;
            }
        }
        else
            for (var y = 0; y < source.Height; ++y)
                foreach (ref var pixel in buffer.DangerousGetRowSpan(y))
                    if (pixel.A != 0)
                        return false;

        return true;
    }

    public static Rectangle FindTransparencyBounds(Image<Rgba32> source)
    {
        int xMin = source.Width, yMin = source.Height, xMax = -1, yMax = -1;
        int width = source.Width, height = source.Height;

        var buffer = source.Frames.RootFrame.PixelBuffer;

        if (Vector.IsHardwareAccelerated)
        {
            var zeroVector = Vector<int>.Zero;
            var vectorSize = Vector<int>.Count;

            Vector<int> alphaMask = new(Unsafe.BitCast<Rgba32, int>(new(0, 0, 0, 255)));
            for (var y = 0; y < height; ++y)
            {
                var rowSpan = buffer.DangerousGetRowSpan(y);
                ref var rowFirst = ref Unsafe.As<Rgba32, int>(ref MemoryMarshal.GetReference(rowSpan));

                var x = 0;
                while (x + vectorSize <= width)
                {
                    if ((Vector.LoadUnsafe(ref rowFirst, (nuint)x) & alphaMask) >> 24 == zeroVector)
                    {
                        x += vectorSize;
                        continue;
                    }

                    for (var i = 0; i < vectorSize; ++i, ++x)
                    {
                        if (Unsafe.As<int, Rgba32>(ref Unsafe.Add(ref rowFirst, x)).A == 0) continue;

                        if (x < xMin) xMin = x;
                        if (x > xMax) xMax = x;
                        if (y < yMin) yMin = y;
                        if (y > yMax) yMax = y;
                    }
                }

                for (; x < rowSpan.Length; ++x)
                {
                    if (Unsafe.As<int, Rgba32>(ref Unsafe.Add(ref rowFirst, x)).A == 0) continue;

                    if (x < xMin) xMin = x;
                    if (x > xMax) xMax = x;
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;
                }
            }
        }
        else
            for (var y = 0; y < height; ++y)
            {
                var srcData = buffer.DangerousGetRowSpan(y);
                for (var x = 0; x < srcData.Length; ++x)
                    if (srcData[x].A != 0)
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