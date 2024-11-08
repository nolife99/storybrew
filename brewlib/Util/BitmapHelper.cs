namespace BrewLib.Util;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Graphics.Compression;

public static class BitmapHelper
{
    public static PinnedBitmap Blur(Bitmap source, int radius, float power)
        => Convolute(source, CalculateGaussianKernel(radius, power));

    public static PinnedBitmap BlurAlpha(Bitmap source, int radius, float power, Color color)
        => ConvoluteAlpha(source, CalculateGaussianKernel(radius, power), color);

    public static void LosslessCompress(string path, ImageCompressor compressor = null)
    {
        LosslessInputSettings defaultSettings = new(7);
        if (compressor is null)
            using (compressor = new IntegratedCompressor())
                compressor.LosslessCompress(path, defaultSettings);
        else
            compressor.LosslessCompress(path, defaultSettings);
    }

    public static void Compress(string path, ImageCompressor compressor = null)
    {
        LossyInputSettings defaultSettings = new(75, 100, 1);
        if (compressor is null)
            using (compressor = new IntegratedCompressor())
                compressor.Compress(path, defaultSettings);
        else
            compressor.Compress(path, defaultSettings);
    }

    public static PinnedBitmap Premultiply(Bitmap source)
    {
        PinnedBitmap result = new(source);
        for (int y = 0, index = 0; y < result.Height; ++y)
        for (var x = 0; x < result.Width; ++x)
        {
            var color = result[y * result.Width + x];

            var alpha = color >> 24 & 0xFF;
            var red = color >> 16 & 0xFF;
            var green = color >> 8 & 0xFF;
            var blue = color & 0xFF;

            var a = alpha / 255f;
            result[index++] = alpha << 24 | (byte)(red * a) << 16 | (byte)(green * a) << 8 | (byte)(blue * a);
        }

        return result;
    }

    public static float[,] CalculateGaussianKernel(int radius, float weight)
    {
        var length = radius * 2 + 1;
        var kernel = new float[length, length];
        var total = 0f;

        var scale = 1 / (weight * weight * MathF.Tau);
        var expFactor = 2 * weight * weight;

        for (var y = 0; y <= radius; ++y)
        for (var x = 0; x <= radius; ++x)
        {
            var value = scale * MathF.Exp(-(x * x + y * y) / expFactor);
            kernel[radius + y, radius + x] = value;
            kernel[radius + y, radius - x] = value;
            kernel[radius - y, radius + x] = value;
            kernel[radius - y, radius - x] = value;

            total += 4 * value;
        }

        var factor = 1 / total;
        for (var y = 0; y < length; ++y)
        for (var x = 0; x < length; ++x)
            kernel[x, y] *= factor;

        return kernel;
    }

    public static PinnedBitmap Convolute(Bitmap source, float[,] kernel)
    {
        var kernelHeight = kernel.GetUpperBound(0) + 1;
        var kernelWidth = kernel.GetUpperBound(1) + 1;

        if ((kernelWidth & 1) == 0 || (kernelHeight & 1) == 0)
            throw new InvalidOperationException("Invalid kernel size");

        var width = source.Width;
        var height = source.Height;

        var halfWidth = kernelWidth >> 1;
        var halfHeight = kernelHeight >> 1;

        PinnedBitmap result = new(width, height);
        using PinnedBitmap src = new(source);

        var srcData = src.AsReadOnlySpan();
        var resultData = result.AsSpan();

        for (int y = 0, index = 0; y < height; ++y)
        for (var x = 0; x < width; ++x)
        {
            float a = 0, r = 0, g = 0, b = 0;
            for (var kernelX = -halfWidth; kernelX <= halfWidth; ++kernelX)
            {
                var pixelX = Math.Clamp(kernelX + x, 0, width - 1);
                for (var kernelY = -halfHeight; kernelY <= halfHeight; ++kernelY)
                {
                    var color = srcData[Math.Clamp(kernelY + y, 0, height - 1) * width + pixelX];
                    var k = kernel[kernelY + halfWidth, kernelX + halfHeight];

                    a += (color >> 24 & 0xFF) * k;
                    r += (color >> 16 & 0xFF) * k;
                    g += (color >> 8 & 0xFF) * k;
                    b += (color & 0xFF) * k;
                }
            }

            if (a == 1) a = 0;
            resultData[index++] = (byte)a << 24 | (byte)r << 16 | (byte)g << 8 | (byte)b;
        }

        return result;
    }

    public static PinnedBitmap ConvoluteAlpha(Bitmap source, float[,] kernel, Color color)
    {
        var kernelHeight = kernel.GetUpperBound(0) + 1;
        var kernelWidth = kernel.GetUpperBound(1) + 1;

        if ((kernelWidth & 1) == 0 || (kernelHeight & 1) == 0)
            throw new InvalidOperationException("Invalid kernel size");

        var width = source.Width;
        var height = source.Height;

        var halfWidth = kernelWidth >> 1;
        var halfHeight = kernelHeight >> 1;

        var rgb = color.R << 16 | color.G << 8 | color.B;

        PinnedBitmap result = new(width, height);
        using PinnedBitmap src = new(source);

        var srcData = src.AsReadOnlySpan();
        var resultData = result.AsSpan();

        for (int y = 0, index = 0; y < height; ++y)
        for (var x = 0; x < width; ++x)
        {
            var a = 0f;
            for (var kernelX = -halfWidth; kernelX <= halfWidth; ++kernelX)
            {
                var pixelX = Math.Clamp(kernelX + x, 0, width - 1);
                for (var kernelY = -halfHeight; kernelY <= halfHeight; ++kernelY)
                {
                    var col = srcData[Math.Clamp(kernelY + y, 0, height - 1) * width + pixelX];
                    a += (col >> 24 & 0xFF) * kernel[kernelY + halfWidth, kernelX + halfHeight];
                }
            }

            resultData[index++] = (byte)a << 24 | rgb;
        }

        return result;
    }

    public static Bitmap FastCloneSection(this Bitmap src, RectangleF sect)
    {
        var srcSize = src.Size;
        if (sect.Left < 0 || sect.Top < 0 || sect.Right > srcSize.Width || sect.Bottom > srcSize.Height ||
            sect.Width <= 0 || sect.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(sect), "Invalid dimensions");

        var srcDat = src.LockBits(new(default, src.Size), ImageLockMode.ReadOnly, src.PixelFormat);
        Rectangle destRect = new(0, 0, (int)sect.Width, (int)sect.Height);
        Bitmap dest = new(destRect.Width, destRect.Height, src.PixelFormat);
        var destDat = dest.LockBits(destRect, ImageLockMode.WriteOnly, src.PixelFormat);

        var pixBit = Image.GetPixelFormatSize(src.PixelFormat) / 8;
        var len = destRect.Width * pixBit;

        var left = (int)sect.X * pixBit;
        var top = (int)sect.Y;
        var srcStride = srcDat.Stride;
        var destStride = destDat.Stride;
        var srcPtr = srcDat.Scan0;
        var destPtr = destDat.Scan0;

        for (var y = 0; y < sect.Height; ++y)
            Native.CopyMemory(srcPtr + (top + y) * srcStride + left, destPtr + y * destStride, len);

        src.UnlockBits(srcDat);
        dest.UnlockBits(destDat);

        return dest;
    }

    public static bool IsFullyTransparent(Image source)
    {
        if (!Image.IsAlphaPixelFormat(source.PixelFormat)) return false;

        using PinnedBitmap src = new(source);
        var srcData = src.AsReadOnlySpan();

        var width = src.Width;
        var height = src.Height;

        for (var y = 0; y < height; ++y)
        for (var x = 0; x < width; ++x)
            if ((srcData[y * width + x] >> 24 & 0xFF) != 0)
                return false;
        return true;
    }

    public static Rectangle FindTransparencyBounds(Image source)
    {
        var size = source.Size;
        if (!Image.IsAlphaPixelFormat(source.PixelFormat)) return new(default, size);

        int xMin = size.Width, yMin = size.Height, xMax = -1, yMax = -1, width = size.Width, height = size.Height;
        using (PinnedBitmap src = new(source))
        {
            var srcData = src.AsReadOnlySpan();
            for (var y = 0; y < height; ++y)
            for (var x = 0; x < width; ++x)
                if ((srcData[y * width + x] >> 24 & 0xFF) != 0)
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

public sealed unsafe class PinnedBitmap : IDisposable, IReadOnlyList<int>
{
    bool disposed;
    int* scan0;

    public PinnedBitmap(int width, int height)
        => Bitmap = new(Width = width, Height = height, width * sizeof(int), PixelFormat.Format32bppArgb,
            (nint)(scan0 = (int*)NativeMemory.Alloc((nuint)((Count = width * height) * sizeof(int)))));

    public PinnedBitmap(Image image) : this(image.Width, image.Height)
    {
        using var graphics = Graphics.FromImage(Bitmap);
        graphics.CompositingMode = CompositingMode.SourceCopy;

        graphics.DrawImage(image, 0, 0, Width, Height);
    }

    public PinnedBitmap(ReadOnlySpan<int> data, int width, int height) : this(width, height)
    {
        fixed (void* pinned = data) Native.CopyMemory(pinned, scan0, Count * sizeof(int));
    }

    public Bitmap Bitmap { get; private set; }

    public int Width { get; private set; }
    public int Height { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public int Count { get; private set; }

    public int this[int pixelIndex]
    {
        get
            => (uint)pixelIndex < (uint)Count ? scan0[pixelIndex]
                : throw new ArgumentOutOfRangeException(nameof(pixelIndex));
        set
        {
            if ((uint)pixelIndex >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(pixelIndex));
            scan0[pixelIndex] = value;
        }
    }

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => new Enumerator(this);
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    public void SetPixel(int x, int y, Color color) => SetPixel(x, y, color.ToArgb());
    public void SetPixel(int x, int y, int color) => SetPixel(y * Width + x, color);
    public void SetPixel(int index, int color) => this[index] = color;
    public void SetPixel(int index, Color color) => this[index] = color.ToArgb();
    public Color GetPixel(int x, int y) => Color.FromArgb(this[y * Width + x]);
    public Color GetPixel(int index) => Color.FromArgb(this[index]);

    public int[] ToArray()
    {
        var array = GC.AllocateUninitializedArray<int>(Count);
        fixed (void* arrAddr = array) Native.CopyMemory(scan0, arrAddr, Count * sizeof(int));
        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<int> AsSpan() => MemoryMarshal.CreateSpan(ref *scan0, Count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<int> AsReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(ref *scan0, Count);

    ~PinnedBitmap() => Dispose(false);

    void Dispose(bool disposing)
    {
        if (disposed) return;
        NativeMemory.Free(scan0);

        if (!disposing) return;

        Bitmap.Dispose();
        Bitmap = null;
        scan0 = null;

        Count = -1;
        Width = -1;
        Height = -1;

        disposed = true;
    }

    struct Enumerator(PinnedBitmap data) : IEnumerator<int>, IEnumerator
    {
        int _index = -1;

        bool IEnumerator.MoveNext()
        {
            var index = _index + 1;
            if (index >= data.Count) return false;
            _index = index;
            return true;
        }

        void IEnumerator.Reset() => _index = -1;
        void IDisposable.Dispose() => _index = int.MaxValue;

        int IEnumerator<int>.Current => data.scan0[_index];
        object IEnumerator.Current => data.scan0[_index];
    }
}