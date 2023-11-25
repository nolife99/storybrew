using System;
using System.Drawing;
using System.Drawing.Imaging;
using BrewLib.Util.Compression;
using System.Runtime.InteropServices;

namespace BrewLib.Util
{
    public static class BitmapHelper
    {
        public static PinnedBitmap Blur(Bitmap source, int radius, float power) => Convolute(source, CalculateGaussianKernel(radius, power));
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
        public static float[,] CalculateGaussianKernel(int radius, float weight)
        {
            var length = radius * 2 + 1;
            var kernel = new float[length, length];
            var total = 0f;

            var scale = 1 / (weight * weight * 2 * MathF.PI);
            for (var y = -radius; y <= radius; ++y) for (var x = -radius; x <= radius; ++x) total += kernel[y + radius, x + radius] = scale * MathF.Exp(-(x * x + y * y) / (2 * weight * weight));
            for (var y = 0; y < length; ++y) for (var x = 0; x < length; ++x) kernel[y, x] = kernel[y, x] / total;
            return kernel;
        }
        public static PinnedBitmap Convolute(Bitmap source, float[,] kernel)
        {
            var kernelHeight = kernel.GetUpperBound(0) + 1;
            var kernelWidth = kernel.GetUpperBound(1) + 1;

            if ((kernelWidth & 1) == 0 || (kernelHeight & 1) == 0) throw new InvalidOperationException("Invalid kernel size");

            var width = source.Width;
            var height = source.Height;

            var index = 0;
            var halfWidth = kernelWidth >> 1;
            var halfHeight = kernelHeight >> 1;

            var pinnedSrc = source.LockBits(new(default, source.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            PinnedBitmap result = new(width, height);

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

                        var col = Marshal.ReadInt32(pinnedSrc.Scan0, sizeof(int) * (pixelY * width + pixelX));
                        var k = kernel[kernelY + halfWidth, kernelX + halfHeight];

                        a += ((col >> 24) & 0xFF) * k;
                        r += ((col >> 16) & 0xFF) * k;
                        g += ((col >> 8) & 0xFF) * k;
                        b += ((col) & 0xFF) * k;
                    }
                }

                var alpha = byte.CreateTruncating(a);
                if (alpha == 1) alpha = 0;

                result.Data[index++] = (alpha << 24) | (byte.CreateTruncating(r) << 16) | (byte.CreateTruncating(g) << 8) | byte.CreateTruncating(b);
            }

            source.UnlockBits(pinnedSrc);
            return result;
        }
        public static PinnedBitmap ConvoluteAlpha(Bitmap source, float[,] kernel, Color color)
        {
            var kernelHeight = kernel.GetUpperBound(0) + 1;
            var kernelWidth = kernel.GetUpperBound(1) + 1;

            if ((kernelWidth & 1) == 0 || (kernelHeight & 1) == 0) throw new InvalidOperationException("Invalid kernel size");

            var width = source.Width;
            var height = source.Height;

            var index = 0;
            var halfWidth = kernelWidth >> 1;
            var halfHeight = kernelHeight >> 1;

            var rgb = (color.R << 16) | (color.G << 8) | color.B;

            var pinnedSrc = source.LockBits(new(default, source.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            PinnedBitmap result = new(width, height); 
            
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

                        var col = Marshal.ReadInt32(pinnedSrc.Scan0, sizeof(int) * (pixelY * width + pixelX));
                        var k = kernel[kernelY + halfWidth, kernelX + halfHeight];
                        a += ((col >> 24) & 0xFF) * k;
                    }
                }
                result.Data[index++] = (byte.CreateTruncating(a) << 24) | rgb;
            }
            return result;
        }

        public static Bitmap FastCloneSection(this Bitmap src, RectangleF sect)
        {
            ArgumentNullException.ThrowIfNull(src);
            if (sect.Left < 0 || sect.Top < 0 || sect.Right > src.Width || sect.Bottom > src.Height || sect.Width <= 0 || sect.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(sect), "Invalid dimensions");

            if (sect.Size == src.PhysicalDimension && sect.Location == default) return src;

            var pixBit = Image.GetPixelFormatSize(src.PixelFormat) / 8;
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

            var data = source.LockBits(new(default, source.Size), ImageLockMode.ReadOnly, source.PixelFormat);
            try
            {
                var pixBit = Image.GetPixelFormatSize(source.PixelFormat) / 8;

                var buf = data.Scan0;
                for (var y = 0; y < data.Height; ++y)
                {
                    for (var x = 0; x < data.Width; ++x)
                    {
                        if (Marshal.ReadByte(buf) != 0)
                        {
                            source.UnlockBits(data);
                            return false;
                        }
                        buf += pixBit;
                    }
                    buf += data.Stride - source.Width * pixBit;
                }
            }
            catch
            {
                source.UnlockBits(data);
                throw;
            }

            return true;
        }
        public static Rectangle FindTransparencyBounds(Bitmap source)
        {
            if (source is null) return default;
            if (!Image.IsAlphaPixelFormat(source.PixelFormat)) return new(default, source.Size);

            var data = source.LockBits(new(default, source.Size), ImageLockMode.ReadOnly, source.PixelFormat);
            int xMin = data.Width, yMin = data.Height, xMax = -1, yMax = -1;

            try
            {
                var pixBit = Image.GetPixelFormatSize(source.PixelFormat) / 8;

                var buf = data.Scan0;
                for (var y = 0; y < data.Height; ++y)
                {
                    var row = buf + (y * data.Stride);
                    for (var x = 0; x < data.Width; ++x) if (Marshal.ReadByte(row, x * pixBit + 3) > 0)
                    {
                        if (x < xMin) xMin = x;
                        if (x > xMax) xMax = x;
                        if (y < yMin) yMin = y;
                        if (y > yMax) yMax = y;
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
            readonly nint addr;

            public PinnedBitmap(int width, int height)
            {
                Data = new int[width * height];
                handle = GCHandle.Alloc(Data, GCHandleType.Pinned);
                Bitmap = new(width, height, width * sizeof(int), PixelFormat.Format32bppArgb, addr = Data.AddrOfPinnedArray());
            }
            public PinnedBitmap(Bitmap bitmap) : this(bitmap.Width, bitmap.Height)
            {
                var data = bitmap.LockBits(new(default, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    Native.CopyMemory(data.Scan0, addr, Data.Length * sizeof(int));
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