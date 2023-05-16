﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using static System.Drawing.Graphics;
using BrewLib.Util.Compression;
using System.Web;

namespace BrewLib.Util
{
    public static class BitmapHelper
    {
        public static PinnedBitmap Blur(Bitmap source, int radius, double power) => Convolute(source, CalculateGaussianKernel(radius, power));

        public static void LosslessCompress(string path, PngCompressor compressor = null)
        {
            var opt = compressor ?? new PngCompressor();
            opt.LosslessCompress(path, new LosslessInputSettings { OptimizationLevel = OptimizationLevel.Level3 });
        }
        public static void Compress(string path, PngCompressor compressor = null)
        {
            var opt = compressor ?? new PngCompressor();
            opt.Compress(path, new LossyInputSettings 
            { 
                Speed = 1,
                MinQuality = 75,
                MaxQuality = 100
            });
        }
        public static PinnedBitmap Premultiply(Bitmap source)
        {
            var result = PinnedBitmap.FromBitmap(source);

            var pixels = source.Width * source.Height;
            for (var index = 0; index < pixels; ++index)
            {
                var color = result.Data[index];

                var alpha = (color >> 24) & 0x000000FF;
                var red = (color >> 16) & 0x000000FF;
                var green = (color >> 8) & 0x000000FF;
                var blue = (color) & 0x000000FF;

                var a = alpha / 255f;
                red = (int)(red * a);
                green = (int)(green * a);
                blue = (int)(blue * a);

                result.Data[index] = (alpha << 24) | (red << 16) | (green << 8) | blue;
            }

            return result;
        }
        public static double[,] CalculateGaussianKernel(int radius, double weight)
        {
            var length = radius * 2 + 1;
            var kernel = new double[length, length];
            var total = 0d;

            var scale = 1 / (2 * Math.PI * (weight * weight));
            for (var y = -radius; y <= radius; ++y) for (var x = -radius; x <= radius; ++x)
            {
                var distance = (x * x + y * y) / (2 * weight * weight);
                var value = kernel[y + radius, x + radius] = scale * Math.Exp(-distance);
                total += value;
            }

            for (var y = 0; y < length; ++y) for (var x = 0; x < length; ++x) kernel[y, x] = kernel[y, x] / total;
            return kernel;
        }
        public static PinnedBitmap Convolute(Bitmap source, double[,] kernel)
        {
            var kernelHeight = kernel.GetUpperBound(0) + 1;
            var kernelWidth = kernel.GetUpperBound(1) + 1;

            if ((kernelWidth % 2) == 0 || (kernelHeight % 2) == 0) throw new InvalidOperationException("Invalid kernel size");

            using (var pinnedSource = PinnedBitmap.FromBitmap(source))
            {
                var width = source.Width;
                var height = source.Height;
                var result = new PinnedBitmap(width, height);

                var index = 0;
                var halfKernelWidth = kernelWidth >> 1;
                var halfKernelHeight = kernelHeight >> 1;

                for (var y = 0; y < height; ++y) for (var x = 0; x < width; ++x)
                {
                    var a = 0d;
                    var r = 0d;
                    var g = 0d;
                    var b = 0d;

                    for (var kernelX = -halfKernelWidth; kernelX <= halfKernelWidth; ++kernelX)
                    {
                        var pixelX = kernelX + x;
                        if (pixelX < 0) pixelX = 0;
                        else if (pixelX >= width) pixelX = width - 1;

                        for (var kernelY = -halfKernelHeight; kernelY <= halfKernelHeight; ++kernelY)
                        {
                            var pixelY = kernelY + y;
                            if (pixelY < 0) pixelY = 0;
                            else if (pixelY >= height) pixelY = height - 1;

                            var col = pinnedSource.Data[pixelY * width + pixelX];
                            var k = kernel[kernelY + halfKernelWidth, kernelX + halfKernelHeight];
                            a += ((col >> 24) & 0x000000FF) * k;
                            r += ((col >> 16) & 0x000000FF) * k;
                            g += ((col >> 8) & 0x000000FF) * k;
                            b += ((col) & 0x000000FF) * k;
                        }
                    }

                    var alphaInt = (int)a;
                    var alpha = (byte)((alphaInt > 255) ? 255 : ((alphaInt < 0) ? 0 : alphaInt));
                    if (alpha == 1) alpha = 0;

                    var redInt = (int)r;
                    var red = (byte)((redInt > 255) ? 255 : ((redInt < 0) ? 0 : redInt));

                    var greenInt = (int)g;
                    var green = (byte)((greenInt > 255) ? 255 : ((greenInt < 0) ? 0 : greenInt));

                    var blueInt = (int)b;
                    var blue = (byte)((blueInt > 255) ? 255 : ((blueInt < 0) ? 0 : blueInt));

                    result.Data[index++] = (alpha << 24) | (red << 16) | (green << 8) | blue;
                }

                return result;
            }
        }
        public static PinnedBitmap ConvoluteAlpha(Bitmap source, double[,] kernel, Color color)
        {
            var kernelHeight = kernel.GetUpperBound(0) + 1;
            var kernelWidth = kernel.GetUpperBound(1) + 1;

            if ((kernelWidth % 2) == 0 || (kernelHeight % 2) == 0) throw new InvalidOperationException("Invalid kernel size");

            using (var pinnedSource = PinnedBitmap.FromBitmap(source))
            {
                var width = source.Width;
                var height = source.Height;
                var result = new PinnedBitmap(width, height);

                var index = 0;
                var halfKernelWidth = kernelWidth >> 1;
                var halfKernelHeight = kernelHeight >> 1;

                var colorRgb = (color.R << 16) | (color.G << 8) | color.B;

                for (var y = 0; y < height; ++y) for (var x = 0; x < width; ++x)
                {
                    var a = 0d;

                    for (var kernelX = -halfKernelWidth; kernelX <= halfKernelWidth; ++kernelX)
                    {
                        var pixelX = kernelX + x;
                        if (pixelX < 0) pixelX = 0;
                        else if (pixelX >= width) pixelX = width - 1;

                        for (var kernelY = -halfKernelHeight; kernelY <= halfKernelHeight; ++kernelY)
                        {
                            var pixelY = kernelY + y;
                            if (pixelY < 0) pixelY = 0;
                            else if (pixelY >= height) pixelY = height - 1;

                            var col = pinnedSource.Data[pixelY * width + pixelX];
                            var k = kernel[kernelY + halfKernelWidth, kernelX + halfKernelHeight];
                            a += ((col >> 24) & 0x000000FF) * k;
                        }
                    }

                    var alphaInt = (int)a;
                    var alpha = (byte)((alphaInt > 255) ? 255 : ((alphaInt < 0) ? 0 : alphaInt));

                    result.Data[index++] = (alpha << 24) | colorRgb;
                }

                return result;
            }
        }
        public static bool IsFullyTransparent(Bitmap source)
        {
            var data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), (ImageLockMode)1, (PixelFormat)2498570);
            unsafe
            {
                var buf = (byte*)data.Scan0;
                for (var y = 0; y < data.Height; ++y)
                {
                    for (var x = 0; x < data.Width; ++x)
                    {
                        var alpha = *buf;
                        if (alpha != 0)
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
        public static Rectangle FindTransparencyBounds(Bitmap bitmap)
        {
            if (bitmap == null) return Rectangle.Empty;

            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int xMin = bitmap.Width, yMin = bitmap.Height, xMax = -1, yMax = -1;

            unsafe
            {
                var buf = (byte*)data.Scan0.ToPointer();

                for (var y = 0; y < bitmap.Height; ++y)
                {
                    var row = buf + (y * data.Stride);
                    for (int x = 0; x < bitmap.Width; ++x)
                    {
                        if (*(row + x * 4 + 3) > 0)
                        {
                            if (x < xMin) xMin = x;
                            if (x > xMax) xMax = x;
                            if (y < yMin) yMin = y;
                            if (y > yMax) yMax = y;
                        }
                    }
                }
            }

            bitmap.UnlockBits(data);

            if (xMin <= xMax && yMin <= yMax) return Rectangle.FromLTRB(xMin, yMin, xMax + 1, yMax + 1);
            return Rectangle.Empty;
        }

        public unsafe class PinnedBitmap : IDisposable
        {
            public readonly Bitmap Bitmap;
            public readonly int[] Data;
            readonly byte* pPixels;

            public PinnedBitmap(int width, int height)
            {
                Data = new int[width * height];
                fixed (int* pData = Data)
                {
                    Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, (IntPtr)pData);
                    pPixels = (byte*)pData;
                }
            }

            public static PinnedBitmap FromBitmap(Bitmap bitmap)
            {
                var result = new PinnedBitmap(bitmap.Width, bitmap.Height);
                using (var graphics = FromImage(result.Bitmap)) graphics.DrawImage(bitmap, 0, 0);
                return result;
            }

            bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Bitmap.Dispose();
            }
        }
    }
}