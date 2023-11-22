using System;
using System.Drawing;
using System.Drawing.Imaging;
using BrewLib.Util.Compression;
using System.Runtime.InteropServices;

namespace BrewLib.Util
{
    public static class BitmapHelper
    {
        public static PinnedBitmap Blur(Bitmap source, int radius, double power) => Convolute(source, CalculateGaussianKernel(radius, power));
        public static PinnedBitmap BlurAlpha(Bitmap source, int radius, double power, Color color) => ConvoluteAlpha(source, CalculateGaussianKernel(radius, power), color);

        public static void LosslessCompress(string path, IntegratedCompressor compressor = null)
        {
            var defaultSettings = new LosslessInputSettings(7);
            if (compressor is null) using (compressor = new IntegratedCompressor()) compressor.LosslessCompress(path, defaultSettings);
            else compressor.LosslessCompress(path, defaultSettings);
        }
        public static void Compress(string path, IntegratedCompressor compressor = null)
        {
            var defaultSettings = new LossyInputSettings(75, 100, 1);
            if (compressor is null) using (compressor = new IntegratedCompressor()) compressor.Compress(path, defaultSettings);
            else compressor.Compress(path, defaultSettings);
        }

        public static PinnedBitmap Premultiply(Bitmap source)
        {
            var result = new PinnedBitmap(source);

            var pixels = source.Width * source.Height;
            for (var index = 0; index < pixels; ++index)
            {
                var color = result.Data[index];

                var alpha = (color >> 24) & 0xFF;
                var red = (color >> 16) & 0xFF;
                var green = (color >> 8) & 0xFF;
                var blue = color & 0xFF;

                var a = alpha / 255f;
                red = (int)(red * a);
                green = (int)(green * a);
                blue = (int)(blue * a);

                result.Data[index] = (alpha << 24) | (red << 16) | (green << 8) | blue;
            }

            return result;
        }
        public static float[,] CalculateGaussianKernel(int radius, double weight)
        {
            var length = radius * 2 + 1;
            var kernel = new float[length, length];
            var total = 0f;

            var scale = 1 / (weight * weight * 2 * Math.PI);
            for (var y = -radius; y <= radius; ++y) for (var x = -radius; x <= radius; ++x)
            {
                var distance = (x * x + y * y) / (2 * weight * weight);
                var value = kernel[y + radius, x + radius] = (float)(scale * Math.Exp(-distance));
                total += value;
            }

            for (var y = 0; y < length; ++y) for (var x = 0; x < length; ++x) kernel[y, x] = kernel[y, x] / total;
            return kernel;
        }
        public static PinnedBitmap Convolute(Bitmap source, float[,] kernel)
        {
            var kernelHeight = kernel.GetUpperBound(0) + 1;
            var kernelWidth = kernel.GetUpperBound(1) + 1;

            if ((kernelWidth & 1) == 0 || (kernelHeight & 1) == 0) throw new InvalidOperationException("Invalid kernel size");

            using (var pinnedSource = new PinnedBitmap(source))
            {
                var width = source.Width;
                var height = source.Height;
                var result = new PinnedBitmap(width, height);

                var index = 0;
                var halfWidth = kernelWidth >> 1;
                var halfHeight = kernelHeight >> 1;

                for (var y = 0; y < height; ++y) for (var x = 0; x < width; ++x)
                {
                    var a = 0f;
                    var r = 0f;
                    var g = 0f;
                    var b = 0f;

                    for (var kernelX = -halfWidth; kernelX <= halfWidth; ++kernelX)
                    {
                        var pixelX = kernelX + x;
                        if (pixelX < 0) pixelX = 0;
                        else if (pixelX >= width) pixelX = width - 1;

                        for (var kernelY = -halfHeight; kernelY <= halfHeight; ++kernelY)
                        {
                            var pixelY = kernelY + y;
                            if (pixelY < 0) pixelY = 0;
                            else if (pixelY >= height) pixelY = height - 1;

                            var col = pinnedSource.Data[pixelY * width + pixelX];
                            var k = kernel[kernelY + halfWidth, kernelX + halfHeight];
                            a += ((col >> 24) & 0xFF) * k;
                            r += ((col >> 16) & 0xFF) * k;
                            g += ((col >> 8) & 0xFF) * k;
                            b += ((col) & 0xFF) * k;
                        }
                    }

                    var alpha = (byte)(a > 255 ? 255 : (a < 0 ? 0 : a));
                    if (alpha == 1) alpha = 0;

                    var red = (byte)(r > 255 ? 255 : (r < 0 ? 0 : r));
                    var green = (byte)(g > 255 ? 255 : (g < 0 ? 0 : g));
                    var blue = (byte)(b > 255 ? 255 : (b < 0 ? 0 : b));

                    result.Data[index++] = (alpha << 24) | (red << 16) | (green << 8) | blue;
                }
                return result;
            }
        }
        public static PinnedBitmap ConvoluteAlpha(Bitmap source, float[,] kernel, Color color)
        {
            var kernelHeight = kernel.GetUpperBound(0) + 1;
            var kernelWidth = kernel.GetUpperBound(1) + 1;

            if ((kernelWidth & 1) == 0 || (kernelHeight & 1) == 0) throw new InvalidOperationException("Invalid kernel size");

            using (var pinnedSource = new PinnedBitmap(source))
            {
                var width = source.Width;
                var height = source.Height;
                var result = new PinnedBitmap(width, height);

                var index = 0;
                var halfWidth = kernelWidth >> 1;
                var halfHeight = kernelHeight >> 1;

                var colorRgb = (color.R << 16) | (color.G << 8) | color.B;
                for (var y = 0; y < height; ++y) for (var x = 0; x < width; ++x)
                {
                    var a = 0f;
                    for (var kernelX = -halfWidth; kernelX <= halfWidth; ++kernelX)
                    {
                        var pixelX = kernelX + x;
                        if (pixelX < 0) pixelX = 0;
                        else if (pixelX >= width) pixelX = width - 1;

                        for (var kernelY = -halfHeight; kernelY <= halfHeight; ++kernelY)
                        {
                            var pixelY = kernelY + y;
                            if (pixelY < 0) pixelY = 0;
                            else if (pixelY >= height) pixelY = height - 1;

                            var col = pinnedSource.Data[pixelY * width + pixelX];
                            var k = kernel[kernelY + halfWidth, kernelX + halfHeight];
                            a += ((col >> 24) & 0xFF) * k;
                        }
                    }

                    var alpha = (byte)(a > 255 ? 255 : (a < 0 ? 0 : a));
                    result.Data[index++] = (alpha << 24) | colorRgb;
                }
                return result;
            }
        }

        public static Bitmap FastCloneSection(this Bitmap src, RectangleF sect)
        {
            if (src is null) throw new ArgumentNullException(nameof(src));
            if (sect.Left < 0 || sect.Top < 0 || sect.Right > src.Width || sect.Bottom > src.Height || sect.Width <= 0 || sect.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(sect), "Invalid dimensions");

            if (sect.Size == src.PhysicalDimension && sect.Location == default) return src;

            var dest = new Bitmap((int)sect.Width, (int)sect.Height, src.PixelFormat);
            var srcDat = src.LockBits(new Rectangle(default, src.Size), ImageLockMode.ReadOnly, src.PixelFormat);
            var destDat = dest.LockBits(new Rectangle(default, dest.Size), ImageLockMode.WriteOnly, src.PixelFormat);

            var pixBit = Image.GetPixelFormatSize(src.PixelFormat) * .125f;
            var len = (int)(dest.Width * pixBit);

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

            var data = source.LockBits(new Rectangle(default, source.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            unsafe
            {
                var buf = (byte*)data.Scan0;
                for (var y = 0; y < data.Height; ++y)
                {
                    for (var x = 0; x < data.Width; ++x)
                    {
                        if (*buf != 0)
                        {
                            source.UnlockBits(data);
                            return false;
                        }
                        buf += 4;
                    }
                    buf += data.Stride - source.Width * 4;
                }
            }
            source.UnlockBits(data);

            return true;
        }
        public static Rectangle FindTransparencyBounds(Bitmap source)
        {
            if (source is null) return default;

            var data = source.LockBits(new Rectangle(default, source.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int xMin = data.Width, yMin = data.Height, xMax = -1, yMax = -1;

            try
            {
                unsafe
                {
                    var buf = (byte*)data.Scan0;
                    for (var y = 0; y < data.Height; ++y)
                    {
                        var row = buf + (y * data.Stride);
                        for (var x = 0; x < data.Width; ++x) if (row[x * 4 + 3] > 0)
                        {
                            if (x < xMin) xMin = x;
                            if (x > xMax) xMax = x;
                            if (y < yMin) yMin = y;
                            if (y > yMax) yMax = y;
                        }
                    }
                }
            }
            finally
            {
                source.UnlockBits(data);
            }

            return xMin <= xMax && yMin <= yMax ? Rectangle.FromLTRB(xMin, yMin, xMax + 1, yMax + 1) : default;
        }

        public sealed class PinnedBitmap : IDisposable
        {
            public readonly Bitmap Bitmap;
            public readonly int[] Data;
            readonly GCHandle handle;

            public PinnedBitmap(int width, int height)
            {
                Data = new int[width * height];
                handle = GCHandle.Alloc(Data, GCHandleType.Pinned);
                Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, Marshal.UnsafeAddrOfPinnedArrayElement(Data, 0));
            }
            public PinnedBitmap(Bitmap bitmap) : this(bitmap.Width, bitmap.Height)
            {
                var data = bitmap.LockBits(new Rectangle(default, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    Marshal.Copy(data.Scan0, Data, 0, Data.Length);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }

            bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                Bitmap?.Dispose();
                if (handle.IsAllocated) handle.Free();
                disposed = true;
            }
        }
    }
}