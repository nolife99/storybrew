using System;
using System.Drawing;
using System.Drawing.Imaging;
using BrewLib.Util.Compression;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

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
        var resultSpan = result.AsSpan();

        for (int y = 0, index = 0; y < source.Height; ++y) for (var x = 0; x < source.Width; ++x)
        {
            var color = resultSpan[y * source.Width + x];

            var alpha = (color >> 24) & 0xFF;
            var red = (color >> 16) & 0xFF;
            var green = (color >> 8) & 0xFF;
            var blue = color & 0xFF;

            var a = alpha / 255f;
            resultSpan[index++] = (alpha << 24) | ((byte)(red * a) << 16) | ((byte)(green * a) << 8) | (byte)(blue * a);
        }

        return result;
    }
    public static float[,] CalculateGaussianKernel(int radius, float weight)
    {
        var length = radius * 2 + 1;
        var kernel = new float[length, length];
        var total = 0f;

        var scale = 1 / (weight * weight * MathF.Tau);
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

        using (PinnedBitmap src = new(source))
        {
            var srcSpan = src.AsReadOnlySpan();
            var destSpan = result.AsSpan();

            for (int y = 0, index = 0; y < height; ++y) for (var x = 0; x < width; ++x)
            {
                float a = 0, r = 0, g = 0, b = 0;
                for (var kernelX = -halfWidth; kernelX <= halfWidth; ++kernelX)
                {
                    var pixelX = Math.Clamp(kernelX + x, 0, width - 1);
                    for (var kernelY = -halfHeight; kernelY <= halfHeight; ++kernelY)
                    {
                        var color = srcSpan[Math.Clamp(kernelY + y, 0, height - 1) * width + pixelX];
                        var k = kernel[kernelY + halfWidth, kernelX + halfHeight];

                        a += ((color >> 24) & 0xFF) * k;
                        r += ((color >> 16) & 0xFF) * k;
                        g += ((color >> 8) & 0xFF) * k;
                        b += (color & 0xFF) * k;
                    }
                }

                var alpha = (byte)a;
                if (alpha == 1) alpha = 0;

                destSpan[index++] = (alpha << 24) | ((byte)r << 16) | ((byte)g << 8) | (byte)b;
            }
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
        using (PinnedBitmap src = new(source))
        {
            var srcSpan = src.AsReadOnlySpan();
            var destSpan = result.AsSpan();

            for (int y = 0, index = 0; y < height; ++y) for (var x = 0; x < width; ++x)
            {
                var a = 0f;
                for (var kernelX = -halfWidth; kernelX <= halfWidth; ++kernelX)
                {
                    var pixelX = Math.Clamp(kernelX + x, 0, width - 1);
                    for (var kernelY = -halfHeight; kernelY <= halfHeight; ++kernelY)
                    {
                        var col = srcSpan[Math.Clamp(kernelY + y, 0, height - 1) * width + pixelX];
                        a += ((col >> 24) & 0xFF) * kernel[kernelY + halfWidth, kernelX + halfHeight];
                    }
                }
                destSpan[index++] = ((byte)a << 24) | rgb;
            }
        }
        return result;
    }
    
    public static Bitmap FastCloneSection(this Bitmap src, RectangleF sect)
    {
        if (sect.Left < 0 || sect.Top < 0 || sect.Right > src.Width || sect.Bottom > src.Height || sect.Width <= 0 || sect.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(sect), "Invalid dimensions");

        var pixBit = Image.GetPixelFormatSize(src.PixelFormat) >> 3;
        var len = (int)(sect.Width * pixBit);

        var srcDat = src.LockBits(new(default, src.Size), ImageLockMode.ReadOnly, src.PixelFormat);
        Bitmap dest = new((int)sect.Width, (int)sect.Height, src.PixelFormat);
        var destDat = dest.LockBits(new(default, dest.Size), ImageLockMode.WriteOnly, src.PixelFormat);

        try
        {
            for (var y = 0; y < sect.Height; ++y) Native.CopyMemory(
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
        if (!Image.IsAlphaPixelFormat(source.PixelFormat)) return false;

        using PinnedBitmap src = new(source);
        var srcSpan = src.AsReadOnlySpan();

        for (var y = 0; y < source.Height; ++y) for (var x = 0; x < source.Width; ++x) if (((srcSpan[y * source.Width + x] >> 24) & 0xFF) != 0) return false;
        return true;
    }
    public static Rectangle FindTransparencyBounds(Bitmap source)
    {
        if (!Image.IsAlphaPixelFormat(source.PixelFormat)) return new(default, source.Size);

        int xMin = source.Width, yMin = source.Height, xMax = -1, yMax = -1;
        using (PinnedBitmap src = new(source))
        {
            var srcSpan = src.AsReadOnlySpan();
            for (var y = 0; y < source.Height; ++y) for (var x = 0; x < source.Width; ++x) if (((srcSpan[y * source.Width + x] >> 24) & 0xFF) != 0)
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

///<summary> Encapsulates and pins bitmaps for high-performance image manipulation. </summary>
public sealed unsafe class PinnedBitmap : IDisposable, IReadOnlyList<int>
{
    int* scan0;
    bool disposed;

    ///<summary> The underlying bitmap. </summary>
    public Bitmap Bitmap { get; private set; }

    ///<summary> The total amount of pixels of the underlying bitmap. </summary>
    public int Count { get; private set; }

    ///<summary> The width, in pixels, of the underlying bitmap. </summary>
    public int Width { get; private set; }

    ///<summary> The height, in pixels, of the underlying bitmap. </summary>
    public int Height { get; private set; }

    ///<summary> Gets or sets the pixel color at the given pixel index as a 32-bit ARGB channel (AARRGGBB). </summary>
    ///<exception cref="IndexOutOfRangeException"> The bitmap was disposed or the provided coordinates are out of bounds. </exception>
    public int this[int pixelIndex]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => unchecked(AsReadOnlySpan()[pixelIndex]);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => AsSpan()[pixelIndex] = unchecked(value);
    }

    ///<summary> Creates a new pinned bitmap from the given dimensions. </summary>
    public PinnedBitmap(int width, int height) => Bitmap = new(Width = width, Height = height, width << 2, PixelFormat.Format32bppArgb, 
        (nint)(scan0 = (int*)NativeMemory.Alloc((uint)(Count = width * height), sizeof(int))));

    ///<summary> Creates a new pinned bitmap from a copy of the given image. </summary>
    public PinnedBitmap(Image image) : this(image.Width, image.Height)
    {
        if (image.PixelFormat is PixelFormat.Format32bppArgb && image is Bitmap bitmap)
        {
            var data = bitmap.LockBits(new(default, image.Size), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                Native.CopyMemory(data.Scan0, (nint)scan0, Count << 2);
                return;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        using var graphics = System.Drawing.Graphics.FromImage(Bitmap);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;

        graphics.DrawImage(image, 0, 0, image.Width, image.Height);
    }

    ///<summary> Creates a new pinned bitmap from a copy of the given 32-bit ARGB color data and dimensions. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PinnedBitmap(ReadOnlySpan<int> data, int width, int height) : this(width, height) => data.CopyTo(AsSpan());

    ///<summary> Sets the pixel color at the given coordinates. </summary>
    ///<param name="x"> The X coordinate of the pixel. </param>
    ///<param name="y"> The Y coordinate of the pixel. </param>
    ///<param name="color"> The new color of the pixel. </param>
    ///<exception cref="IndexOutOfRangeException"> The bitmap was disposed or the provided coordinates are out of bounds. </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPixel(int x, int y, Color color) => SetPixel(x, y, color.ToArgb());

    ///<summary> Sets the pixel color at the given coordinates. </summary>
    ///<param name="x"> The X coordinate of the pixel. </param>
    ///<param name="y"> The Y coordinate of the pixel. </param>
    ///<param name="color"> The new color of the pixel as a 32-bit ARGB channel (AARRGGBB). </param>
    ///<exception cref="IndexOutOfRangeException"> The bitmap was disposed or the provided coordinates are out of bounds. </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPixel(int x, int y, int color) => SetPixel(y * Width + x, color);

    ///<summary> Sets the pixel color at the given index. </summary>
    ///<param name="index"> An index into the underlying bitmap. </param>
    ///<param name="color"> The new color of the pixel as a 32-bit ARGB channel (AARRGGBB). </param>
    ///<exception cref="IndexOutOfRangeException"> The bitmap was disposed or the provided coordinates are out of bounds. </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPixel(int index, int color) => this[index] = color;

    ///<summary> Sets the pixel color at the given index. </summary>
    ///<param name="index"> An index into the underlying bitmap. </param>
    ///<param name="color"> The new color of the pixel. </param>
    ///<exception cref="IndexOutOfRangeException"> The bitmap was disposed or the provided coordinates are out of bounds. </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPixel(int index, Color color) => this[index] = color.ToArgb();

    ///<summary> Gets the pixel color at the given coordinates. </summary>
    ///<param name="x"> The X coordinate of the pixel. </param>
    ///<param name="y"> The Y coordinate of the pixel. </param>
    ///<exception cref="IndexOutOfRangeException"> The bitmap was disposed or the provided coordinates are out of bounds. </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color GetPixel(int x, int y) => Color.FromArgb(this[y * Width + x]);

    ///<summary> Gets the pixel color at the given index. </summary>
    ///<param name="index"> An index into the underlying bitmap. </param>
    ///<exception cref="IndexOutOfRangeException"> The bitmap was disposed or the provided coordinates are out of bounds. </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color GetPixel(int index) => Color.FromArgb(this[index]);

    ///<summary> Gets a collection of 32-bit color channels that represent the underlying bitmap. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int[] ToArray()
    {
        var array = GC.AllocateUninitializedArray<int>(Count);
        fixed (void* arrAddr = &MemoryMarshal.GetArrayDataReference(array)) Native.CopyMemory((nint)scan0, (nint)arrAddr, Count << 2);
        return array;
    }

    ///<summary> Gets a span view of the entire bitmap. </summary>
    ///<remarks> It is highly recommended to use this method in a loop instead of directly accessing the pixel data. </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<int> AsSpan() => MemoryMarshal.CreateSpan(ref *scan0, Count);

    ///<summary> Gets a read-only span view of the entire bitmap. </summary>
    ///<remarks> It is highly recommended to use this method in a loop instead of directly accessing the pixel data. </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<int> AsReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(ref *scan0, Count);

    ///<summary> Releases the underlying bitmap and frees the allocated memory. </summary>
    ///<remarks> Disposing invalidates all data. Attempts to read/write after disposal can cause fatal errors. </remarks>
    public void Dispose()
    {
        if (disposed) return;

        Count = int.MinValue;
        Width = int.MinValue;
        Height = int.MinValue;

        Bitmap.Dispose();
        Bitmap = null;

        NativeMemory.Free(scan0);
        scan0 = null;

        disposed = true;
    }

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => new Enumerator(this);
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    struct Enumerator(PinnedBitmap data) : IEnumerator<int>, IEnumerator
    {
        int _index = -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IEnumerator.MoveNext()
        {
            var index = _index + 1;
            if (index < data.Count)
            {
                _index = index;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IEnumerator.Reset() => _index = -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IDisposable.Dispose() => _index = int.MaxValue;

        int IEnumerator<int>.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data.scan0[_index];
        }

        object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data.scan0[_index];
        }
    }
}