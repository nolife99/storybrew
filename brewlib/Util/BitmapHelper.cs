using System;
using System.Drawing;
using System.Drawing.Imaging;
using BrewLib.Util.Compression;
using System.Collections.Generic;
using System.Collections;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace BrewLib.Util;

public static class BitmapHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PinnedBitmap Blur(Bitmap source, int radius, float power) => Convolute(source, CalculateGaussianKernel(radius, power));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PinnedBitmap BlurAlpha(Bitmap source, int radius, float power, Color color) => ConvoluteAlpha(source, CalculateGaussianKernel(radius, power), color);

    public static void LosslessCompress(string path, ImageCompressor compressor = null)
    {
        LosslessInputSettings defaultSettings = new(7);
        if (compressor is null) using (compressor = new IntegratedCompressor()) compressor.LosslessCompress(path, defaultSettings);
        else compressor.LosslessCompress(path, defaultSettings);
    }
    public static void Compress(string path, ImageCompressor compressor = null)
    {
        LossyInputSettings defaultSettings = new(75, 100, 1);
        if (compressor is null) using (compressor = new IntegratedCompressor()) compressor.Compress(path, defaultSettings);
        else compressor.Compress(path, defaultSettings);
    }

    public static PinnedBitmap Premultiply(Bitmap source)
    {
        PinnedBitmap result = new(source);
        for (int y = 0, index = 0; y < source.Height; ++y) for (var x = 0; y < source.Width; ++x)
        {
            var color = result[y * source.Width + x];

            var alpha = (color >> 24) & 0xFF;
            var red = (color >> 16) & 0xFF;
            var green = (color >> 8) & 0xFF;
            var blue = color & 0xFF;

            var a = alpha / 255f;
            red = byte.CreateTruncating(red * a);
            green = byte.CreateTruncating(green * a);
            blue = byte.CreateTruncating(blue * a);

            result[index++] = (alpha << 24) | (red << 16) | (green << 8) | blue;
        }

        return result;
    }
    public static float[,] CalculateGaussianKernel(int radius, float weight)
    {
        var length = radius * 2 + 1;
        var kernel = new float[length, length];
        var total = 0f;

        var scale = 1 / (weight * weight * 2 * MathF.PI);
        for (var y = -radius; y <= radius; ++y) for (var x = -radius; x <= radius; ++x) total += kernel[y + radius, x + radius] = scale * MathF.Exp(-(x * x + y * y) / (2 * weight * weight));
        for (var y = 0; y < length; ++y) for (var x = 0; x < length; ++x) kernel[y, x] /= total;
        return kernel;
    }
    public static PinnedBitmap Convolute(Bitmap source, float[,] kernel)
    {
        var kernelHeight = kernel.GetUpperBound(0) + 1;
        var kernelWidth = kernel.GetUpperBound(1) + 1;

        if ((kernelWidth & 1) == 0 || (kernelHeight & 1) == 0) throw new InvalidOperationException("Invalid kernel size");

        var width = source.Width;
        var height = source.Height;
        
        var halfWidth = kernelWidth >> 1;
        var halfHeight = kernelHeight >> 1;

        PinnedBitmap result = new(width, height);
        using (PinnedBitmap src = new(source)) for (int y = 0, index = 0; y < height; ++y) for (var x = 0; x < width; ++x)
        {
            float a = 0f, r = 0f, g = 0f, b = 0f;
            for (var kernelX = -halfWidth; kernelX <= halfWidth; ++kernelX)
            {
                var pixelX = osuTK.MathHelper.Clamp(kernelX + x, 0, width - 1);
                for (var kernelY = -halfHeight; kernelY <= halfHeight; ++kernelY)
                {
                    var color = src[osuTK.MathHelper.Clamp(kernelY + y, 0, height - 1) * width + pixelX];
                    var k = kernel[kernelY + halfWidth, kernelX + halfHeight];

                    a += ((color >> 24) & 0xFF) * k;
                    r += ((color >> 16) & 0xFF) * k;
                    g += ((color >> 8) & 0xFF) * k;
                    b += (color & 0xFF) * k;
                }
            }

            var alpha = byte.CreateTruncating(a);
            if (alpha == 1) alpha = 0;

            result[index++] = (alpha << 24) | (byte.CreateTruncating(r) << 16) | (byte.CreateTruncating(g) << 8) | byte.CreateTruncating(b);
        }

        return result;
    }
    public static PinnedBitmap ConvoluteAlpha(Bitmap source, float[,] kernel, Color color)
    {
        var kernelHeight = kernel.GetUpperBound(0) + 1;
        var kernelWidth = kernel.GetUpperBound(1) + 1;

        if ((kernelWidth & 1) == 0 || (kernelHeight & 1) == 0) throw new InvalidOperationException("Invalid kernel size");

        var width = source.Width;
        var height = source.Height;

        var halfWidth = kernelWidth >> 1;
        var halfHeight = kernelHeight >> 1;

        var rgb = (color.R << 16) | (color.G << 8) | color.B;

        PinnedBitmap result = new(width, height);
        using (PinnedBitmap src = new(source)) for (int y = 0, index = 0; y < height; ++y) for (var x = 0; x < width; ++x)
        {
            var a = 0f;
            for (var kernelX = -halfWidth; kernelX <= halfWidth; ++kernelX)
            {
                var pixelX = osuTK.MathHelper.Clamp(kernelX + x, 0, width - 1);
                for (var kernelY = -halfHeight; kernelY <= halfHeight; ++kernelY)
                {
                    var col = src[osuTK.MathHelper.Clamp(kernelY + y, 0, height - 1) * width + pixelX];
                    a += ((col >> 24) & 0xFF) * kernel[kernelY + halfWidth, kernelX + halfHeight];
                }
            }
            result[index++] = (byte.CreateTruncating(a) << 24) | rgb;
        }
        return result;
    }
    
    public static Bitmap FastCloneSection(this Bitmap src, RectangleF sect)
    {
        ArgumentNullException.ThrowIfNull(src);
        if (sect.Left < 0 || sect.Top < 0 || sect.Right > src.Width || sect.Bottom > src.Height || sect.Width <= 0 || sect.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(sect), "Invalid dimensions");

        if (sect.Size == src.PhysicalDimension && sect.Location == default) return src;

        var pixBit = Image.GetPixelFormatSize(src.PixelFormat) >> 3;
        var len = (int)(sect.Width * pixBit);

        var srcDat = src.LockBits(new(default, src.Size), ImageLockMode.ReadOnly, src.PixelFormat);
        Bitmap dest = new((int)sect.Width, (int)sect.Height, src.PixelFormat);
        var destDat = dest.LockBits(new(default, dest.Size), ImageLockMode.WriteOnly, src.PixelFormat);

        try
        {
            for (var y = 0; y < destDat.Height; ++y) Native.CopyMemory(
                srcDat.Scan0 + (int)(sect.Y + y) * srcDat.Stride + (int)(sect.X * pixBit), destDat.Scan0 + y * destDat.Stride, len);
        }
        finally
        {
            src.UnlockBits(srcDat);
            dest.UnlockBits(destDat);
        }

        return dest;
    }

    public static bool IsFullyTransparent(Bitmap source)
    {
        if (source is null) return true;
        if (!Image.IsAlphaPixelFormat(source.PixelFormat)) return false;

        using PinnedBitmap src = new(source);
        for (var y = 0; y < source.Height; ++y) for (var x = 0; x < source.Width; ++x) if (((src[y * source.Width + x] >> 24) & 0xFF) != 0) return false;

        return true;
    }
    public static Rectangle FindTransparencyBounds(Bitmap source)
    {
        if (source is null) return default;
        if (!Image.IsAlphaPixelFormat(source.PixelFormat)) return new(default, source.Size);

        int xMin = source.Width, yMin = source.Height, xMax = -1, yMax = -1;

        using (PinnedBitmap src = new(source)) for (var y = 0; y < source.Height; ++y) for (var x = 0; x < source.Width; ++x) if (((src[y * source.Width + x] >> 24) & 0xFF) > 0)
        {
            if (x < xMin) xMin = x;
            if (x > xMax) xMax = x;
            if (y < yMin) yMin = y;
            if (y > yMax) yMax = y;
        }

        return xMin <= xMax && yMin <= yMax ? Rectangle.FromLTRB(xMin, yMin, xMax + 1, yMax + 1) : default;
    }
}
public unsafe sealed class PinnedBitmap : IDisposable, IReadOnlyList<int>
{
    public Bitmap Bitmap { get; private set; }
    public int Count { get; private set; }

    readonly int[] data;
    bool disposed;

    public int this[int pixelIndex]
    {
        get => new ReadOnlySpan<int>(data, 0, Count)[pixelIndex];
        set => new Span<int>(data, 0, Count)[pixelIndex] = value;
    }

    public PinnedBitmap(int width, int height)
    {
        data = ArrayPool<int>.Shared.Rent(Count = width * height);
        new Span<int>(data, 0, Count).Clear();
        Bitmap = new(width, height, width << 2, PixelFormat.Format32bppArgb, data.AddrOfPinnedArray());
    }
    public PinnedBitmap(Bitmap bitmap) : this(bitmap.Width, bitmap.Height)
    {
        using (var graphics = System.Drawing.Graphics.FromImage(Bitmap)) graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
    }

    public void Dispose()
    {
        if (disposed) return;
        Count = 0;

        Bitmap.Dispose();
        Bitmap = null;

        ArrayPool<int>.Shared.Return(data);
        disposed = true;
    }

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => ((IEnumerable<int>)data).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => data.GetEnumerator();
}