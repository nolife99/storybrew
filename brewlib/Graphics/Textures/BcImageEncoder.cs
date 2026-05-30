namespace BrewLib.Graphics.Textures;

using System;
using System.Runtime.InteropServices;
using BasisBlockEncoder;
using BrewLib.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

static class BcImageEncoder
{
    const int BlockSourceBytes = 64;

    public static bool TryPlan(Image<Rgba32> image,
        TextureCompressionFormats supported,
        in BcCompressionPolicy policy,
        TextureOptions options,
        out BcPlan plan)
    {
        plan = default;

        if (options.GenerateMipmaps) return false;
        if ((long)image.Width * image.Height < policy.MinArea) return false;

        if (image.IsFullyOpaque())
        {
            if ((supported & TextureCompressionFormats.Bc1) == 0) return false;
            plan = new(BcFormat.Bc1, 0, 0, image.Width, image.Height, false);
            return true;
        }

        if ((supported & TextureCompressionFormats.Bc7) == 0) return false;

        int x = 0, y = 0, w = image.Width, h = image.Height;
        if (policy.TrimTransparent)
        {
            var bounds = BitmapHelper.FindTransparencyBounds(image);
            if (bounds.Width <= 0 || bounds.Height <= 0) return false;
            if (bounds.Width < w || bounds.Height < h)
            {
                x = bounds.X;
                y = bounds.Y;
                w = bounds.Width;
                h = bounds.Height;
            }
        }

        plan = new(BcFormat.Bc7, x, y, w, h, true);
        return true;
    }

    public static void EncodeBand(Image<Rgba32> image,
        in BcPlan plan,
        int blockRowStart,
        int blockRowCount,
        scoped Span<byte> dst)
    {
        var fmt = plan.Format;
        var flags = fmt == BcFormat.Bc1
            ? (uint)Bc1Quality.HighQuality
            : (uint)Bc7Flags.HighQuality;
        var bb = BlockEncoder.BlockBytes(fmt);
        var bw = BlockEncoder.BlocksWide(plan.ContentW);
        int cx = plan.ContentX, cy = plan.ContentY, cw = plan.ContentW, ch = plan.ContentH;

        Span<byte> block = stackalloc byte[BlockSourceBytes];
        var block32 = MemoryMarshal.Cast<byte, uint>(block);
        for (var rel = 0; rel < blockRowCount; rel++)
        {
            var byi = blockRowStart + rel;
            int y0 = cy + byi * 4, maxY = cy + ch - 1;
            var r0 = image.DangerousGetPixelRowMemory(Math.Min(y0,     maxY)).Span;
            var r1 = image.DangerousGetPixelRowMemory(Math.Min(y0 + 1, maxY)).Span;
            var r2 = image.DangerousGetPixelRowMemory(Math.Min(y0 + 2, maxY)).Span;
            var r3 = image.DangerousGetPixelRowMemory(Math.Min(y0 + 3, maxY)).Span;

            for (var bxi = 0; bxi < bw; bxi++)
            {
                for (var i = 0; i < 4; i++)
                {
                    var sx = Math.Min(cx + bxi * 4 + i, cx + cw - 1);
                    block32[i]      = r0[sx].PackedValue;
                    block32[i + 4]  = r1[sx].PackedValue;
                    block32[i + 8]  = r2[sx].PackedValue;
                    block32[i + 12] = r3[sx].PackedValue;
                }

                BlockEncoder.EncodeBlock(fmt, block, dst.Slice((rel * bw + bxi) * bb, bb), flags);
            }
        }
    }
}