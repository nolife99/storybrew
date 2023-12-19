using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BrewLib.Util;

// Have to find a way to use this properly first
public unsafe class GaussianBlur
{
    readonly int[] _alpha, _red, _green, _blue;
    readonly int _width, _height;

    readonly ParallelOptions _pOptions = new() { MaxDegreeOfParallelism = 16 };

    public GaussianBlur(Bitmap image)
    {
        Rectangle rct = new(0, 0, image.Width, image.Height);
        var source = GC.AllocateUninitializedArray<int>(rct.Width * rct.Height);
        var bits = image.LockBits(rct, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        Marshal.Copy(bits.Scan0, source, 0, source.Length);
        image.UnlockBits(bits);

        _width = image.Width;
        _height = image.Height;

        _alpha = GC.AllocateUninitializedArray<int>(rct.Width * rct.Height);
        _red = GC.AllocateUninitializedArray<int>(rct.Width * rct.Height);
        _green = GC.AllocateUninitializedArray<int>(rct.Width * rct.Height);
        _blue = GC.AllocateUninitializedArray<int>(rct.Width * rct.Height);

        Parallel.For(0, source.Length, _pOptions, i =>
        {
            _alpha[i] = (int)((source[i] & 0xff000000) >> 24);
            _red[i] = (source[i] & 0xff0000) >> 16;
            _green[i] = (source[i] & 0x00ff00) >> 8;
            _blue[i] = source[i] & 0x0000ff;
        });
    }

    public PinnedBitmap Process(int radius, int weight)
    {
        var newAlpha = stackalloc int[_width * _height];
        var newRed = stackalloc int[_width * _height];
        var newGreen = stackalloc int[_width * _height];
        var newBlue = stackalloc int[_width * _height];

        Parallel.Invoke(
            () => gaussBlur_4(ref MemoryMarshal.GetArrayDataReference(_alpha), newAlpha, radius, weight),
            () => gaussBlur_4(ref MemoryMarshal.GetArrayDataReference(_red), newRed, radius, weight),
            () => gaussBlur_4(ref MemoryMarshal.GetArrayDataReference(_green), newGreen, radius, weight),
            () => gaussBlur_4(ref MemoryMarshal.GetArrayDataReference(_blue), newBlue, radius, weight));

        PinnedBitmap dest = new(_width, _height);
        Parallel.For(0, dest.Count, _pOptions, i => dest[i] = 
            ((byte)newAlpha[i] << 24) |
            ((byte)newRed[i] << 16) |
            ((byte)newGreen[i] << 8) |
            (byte)newBlue[i]);

        return dest;
    }

    void gaussBlur_4(ref int source, int* dest, int r, int n)
    {
        var bxs = boxesForGauss(r, n);
        fixed (int* addr = &source)
        {
            boxBlur_4(addr, dest, _width, _height, (bxs[0] - 1) / 2);
            boxBlur_4(dest, addr, _width, _height, (bxs[1] - 1) / 2);
            boxBlur_4(addr, dest, _width, _height, (bxs[2] - 1) / 2);
        }
    }
    static int[] boxesForGauss(int sigma, int n)
    {
        var wIdeal = Math.Sqrt((12 * sigma * sigma / n) + 1);
        var wl = (int)wIdeal;
        if ((wl & 1) != 1) --wl;
        var wu = wl + 2;

        var mIdeal = (double)(12 * sigma * sigma - n * wl * wl - 4 * n * wl - 3 * n) / (-4 * wl - 4);
        var m = Math.Round(mIdeal);

        var sizes = GC.AllocateUninitializedArray<int>(n);
        for (var i = 0; i < n; i++) sizes[i] = i < m ? wl : wu;
        return sizes;
    }
    void boxBlur_4(int* source, int* dest, int w, int h, int r)
    {
        Native.CopyMemory(source, dest, _width * _height << 2);
        boxBlurH_4(dest, source, w, h, r);
        boxBlurT_4(source, dest, w, h, r);
    }
    void boxBlurH_4(int* source, int* dest, int w, int h, int r)
    {
        var iar = (double)1 / (r + r + 1);
        Parallel.For(0, h, _pOptions, i =>
        {
            var ti = i * w;
            var li = ti;
            var ri = ti + r;
            var fv = source[ti];
            var lv = source[ti + w - 1];
            var val = (r + 1) * fv;
            for (var j = 0; j < r; j++) val += source[ti + j];
            for (var j = 0; j <= r; j++)
            {
                val += source[ri++] - fv;
                dest[ti++] = (int)Math.Round(val * iar);
            }
            for (var j = r + 1; j < w - r; j++)
            {
                val += source[ri++] - dest[li++];
                dest[ti++] = (int)Math.Round(val * iar);
            }
            for (var j = w - r; j < w; j++)
            {
                val += lv - source[li++];
                dest[ti++] = (int)Math.Round(val * iar);
            }
        });
    }

    void boxBlurT_4(int* source, int* dest, int w, int h, int r)
    {
        var iar = (double)1 / (r + r + 1);
        Parallel.For(0, w, _pOptions, i =>
        {
            var ti = i;
            var li = ti;
            var ri = ti + r * w;
            var fv = source[ti];
            var lv = source[ti + w * (h - 1)];
            var val = (r + 1) * fv;
            for (var j = 0; j < r; j++) val += source[ti + j * w];
            for (var j = 0; j <= r; j++)
            {
                val += source[ri] - fv;
                dest[ti] = (int)Math.Round(val * iar);
                ri += w;
                ti += w;
            }
            for (var j = r + 1; j < h - r; j++)
            {
                val += source[ri] - source[li];
                dest[ti] = (int)Math.Round(val * iar);
                li += w;
                ri += w;
                ti += w;
            }
            for (var j = h - r; j < h; j++)
            {
                val += lv - source[li];
                dest[ti] = (int)Math.Round(val * iar);
                li += w;
                ti += w;
            }
        });
    }
}