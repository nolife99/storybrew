namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;

/// <summary>
///     RGBA8 -> BC7/BC1 block encoder producing tightly-packed blocks. (Name kept for continuity; it now also does
///     BC1 for opaque content.) BC7 is 16 bytes per 4x4 block with full alpha; BC1 is 8 bytes per block, opaque, and
///     much faster to encode — a good fit for fully-opaque frames where it also halves BC7's footprint (8x vs RGBA8).
///
///     Two interchangeable backends sit behind <see cref="Encode"/>:
///     <list type="bullet">
///         <item><b>Managed</b> (default) — pure-C# BCnEncoder.NET. No native dependency; allocates per call, so it's
///         slow and GC-heavy. Fine for bring-up.</item>
///         <item><b>Ispc</b> — Intel's ISPC Texture Compressor (SIMD). Encodes into the caller's buffer (no per-call
///         GC) and is far faster. Flip <see cref="ActiveBackend"/> once the native lib is available.</item>
///     </list>
///     The encode is pure CPU with no graphics dependency and is meant to run on a worker thread.
/// </summary>
static unsafe class Bc7Encoder
{
    public const int BlockDim = 4;

    public enum BcFormat
    {
        Bc7,
        Bc1
    }

    public enum Quality
    {
        UltraFast,
        VeryFast,
        Fast,
        Basic,
        Slow
    }

    public enum Backend
    {
        Managed,
        Ispc
    }

    public static Backend ActiveBackend { get; set; } = Backend.Managed;

    /// <summary>Managed backend only: encode a texture's blocks in parallel. Turn off if many textures encode at once.</summary>
    public static bool ManagedParallel { get; set; } = true;

    [ThreadStatic] static byte[] scratch;
    [ThreadStatic] static BcEncoder managedEncoder;

    public static int BlockBytes(BcFormat format) => format == BcFormat.Bc1 ? 8 : 16;
    public static int BlocksWide(int width) => (width + BlockDim - 1) / BlockDim;
    public static int BlocksHigh(int height) => (height + BlockDim - 1) / BlockDim;

    public static int TightRowBytes(int width, BcFormat format) => BlocksWide(width) * BlockBytes(format);
    public static int EncodedSize(int width, int height, BcFormat format) => TightRowBytes(width, format) * BlocksHigh(height);

    /// <summary>
    ///     Encodes RGBA8 to tightly-packed blocks of <paramref name="format"/> in <paramref name="dst"/>
    ///     (>= <see cref="EncodedSize"/>). <paramref name="srcStride"/> is the source row pitch (>= width*4); pass a
    ///     slice + the full stride to encode a sub-rect. <paramref name="hasAlpha"/> only affects the ISPC BC7 profile.
    /// </summary>
    public static void Encode(ReadOnlySpan<byte> rgba,
        int width,
        int height,
        int srcStride,
        Span<byte> dst,
        BcFormat format = BcFormat.Bc7,
        Quality quality = Quality.Basic,
        bool hasAlpha = true)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (srcStride < width * 4) throw new ArgumentOutOfRangeException(nameof(srcStride), "srcStride must be at least width * 4");
        if ((long)srcStride * (height - 1) + (long)width * 4 > rgba.Length) throw new ArgumentException("rgba is shorter than the described image", nameof(rgba));
        if (dst.Length < EncodedSize(width, height, format)) throw new ArgumentException("dst is smaller than EncodedSize", nameof(dst));

        if (ActiveBackend == Backend.Ispc) EncodeIspc(rgba, width, height, srcStride, dst, format, quality, hasAlpha);
        else EncodeManaged(rgba, width, height, srcStride, dst, format, quality);
    }

    // ----- Managed backend: BCnEncoder.NET -------------------------------------------------------------------

    static void EncodeManaged(ReadOnlySpan<byte> rgba, int width, int height, int srcStride, Span<byte> dst, BcFormat format, Quality quality)
    {
        // BCnEncoder needs a contiguous width*height RGBA8 buffer (no row padding) and pads partial edge blocks
        // internally with the edge color, so only stride compaction is needed here.
        var tightImageBytes = width * height * 4;
        ReadOnlySpan<byte> contiguous;
        if (srcStride == width * 4)
            contiguous = rgba.Slice(0, tightImageBytes);
        else
        {
            if (scratch is null || scratch.Length < tightImageBytes)
                scratch = GC.AllocateUninitializedArray<byte>(tightImageBytes);
            var rowBytes = width * 4;
            for (var y = 0; y < height; ++y)
                rgba.Slice(y * srcStride, rowBytes).CopyTo(scratch.AsSpan(y * rowBytes, rowBytes));
            contiguous = scratch.AsSpan(0, tightImageBytes);
        }

        var encoder = managedEncoder ??= new BcEncoder();
        encoder.OutputOptions.Format = format == BcFormat.Bc1 ? CompressionFormat.Bc1 : CompressionFormat.Bc7;
        encoder.OutputOptions.Quality = MapQuality(quality);
        encoder.OutputOptions.GenerateMipMaps = false;
        encoder.OutputOptions.MaxMipMapLevel = 1;
        encoder.Options.IsParallel = ManagedParallel;

        var raw = encoder.EncodeToRawBytes(contiguous, width, height, PixelFormat.Rgba32, 0, out _, out _);
        Debug.Assert(raw.Length == EncodedSize(width, height, format), "BCnEncoder output size mismatch");
        raw.AsSpan().CopyTo(dst);
    }

    static CompressionQuality MapQuality(Quality quality) => quality switch
    {
        Quality.UltraFast or Quality.VeryFast or Quality.Fast => CompressionQuality.Fast,
        Quality.Slow => CompressionQuality.BestQuality,
        _ => CompressionQuality.Balanced
    };

    // ----- ISPC backend: Intel ISPC Texture Compressor (SIMD) ------------------------------------------------

    static void EncodeIspc(ReadOnlySpan<byte> rgba, int width, int height, int srcStride, Span<byte> dst, BcFormat format, Quality quality, bool hasAlpha)
    {
        Bc7EncSettings settings = default;
        if (format == BcFormat.Bc7) SelectProfile(quality, hasAlpha, &settings);

        var paddedW = BlocksWide(width) * BlockDim;
        var paddedH = BlocksHigh(height) * BlockDim;

        if (paddedW == width && paddedH == height)
        {
            fixed (byte* src = rgba)
            fixed (byte* d = dst)
            {
                var surface = new RgbaSurface { ptr = src, width = width, height = height, stride = srcStride };
                if (format == BcFormat.Bc1) Ispc.CompressBlocksBC1(&surface, d);
                else Ispc.CompressBlocksBC7(&surface, d, &settings);
            }

            return;
        }

        // ISPC iterates whole 4x4 blocks, so pad to a multiple of 4 and replicate the last row/column.
        var paddedStride = paddedW * 4;
        var scratchNeeded = paddedStride * paddedH;
        if (scratch is null || scratch.Length < scratchNeeded)
            scratch = GC.AllocateUninitializedArray<byte>(scratchNeeded);

        fixed (byte* srcBase = rgba)
        fixed (byte* padBase = scratch)
        fixed (byte* d = dst)
        {
            for (var y = 0; y < paddedH; ++y)
            {
                var srcY = y < height ? y : height - 1;
                var srcRow = srcBase + (long)srcY * srcStride;
                var dstRow = padBase + (long)y * paddedStride;

                Buffer.MemoryCopy(srcRow, dstRow, paddedStride, (long)width * 4);

                if (paddedW > width)
                {
                    var last = *(uint*)(srcRow + (long)(width - 1) * 4);
                    var fill = (uint*)(dstRow + (long)width * 4);
                    for (var x = width; x < paddedW; ++x) *fill++ = last;
                }
            }

            var surface = new RgbaSurface { ptr = padBase, width = paddedW, height = paddedH, stride = paddedStride };
            if (format == BcFormat.Bc1) Ispc.CompressBlocksBC1(&surface, d);
            else Ispc.CompressBlocksBC7(&surface, d, &settings);
        }
    }

    static void SelectProfile(Quality quality, bool hasAlpha, Bc7EncSettings* s)
    {
        if (hasAlpha)
            switch (quality)
            {
                case Quality.UltraFast: Ispc.GetProfile_alpha_ultrafast(s); break;
                case Quality.VeryFast: Ispc.GetProfile_alpha_veryfast(s); break;
                case Quality.Fast: Ispc.GetProfile_alpha_fast(s); break;
                case Quality.Slow: Ispc.GetProfile_alpha_slow(s); break;
                default: Ispc.GetProfile_alpha_basic(s); break;
            }
        else
            switch (quality)
            {
                case Quality.UltraFast: Ispc.GetProfile_ultrafast(s); break;
                case Quality.VeryFast: Ispc.GetProfile_veryfast(s); break;
                case Quality.Fast: Ispc.GetProfile_fast(s); break;
                case Quality.Slow: Ispc.GetProfile_slow(s); break;
                default: Ispc.GetProfile_basic(s); break;
            }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RgbaSurface
    {
        public byte* ptr;
        public int width;
        public int height;
        public int stride;
    }

    // Mirrors `struct bc7_enc_settings` from ispc_texcomp.h (64 bytes). Fields are never read managed-side.
    [StructLayout(LayoutKind.Sequential)]
    struct Bc7EncSettings
    {
        public fixed byte mode_selection[4];
        public fixed int refineIterations[8];
        public byte skip_mode2;
        public int fastSkipTreshold_mode1;
        public int fastSkipTreshold_mode3;
        public int fastSkipTreshold_mode7;
        public int mode45_channel0;
        public int refineIterations_channel;
        public int channels;
    }

    static unsafe class Ispc
    {
        const string Lib = "ispc_texcomp";

        [DllImport(Lib)] public static extern void CompressBlocksBC1(RgbaSurface* src, byte* dst);
        [DllImport(Lib)] public static extern void CompressBlocksBC7(RgbaSurface* src, byte* dst, Bc7EncSettings* settings);

        [DllImport(Lib)] public static extern void GetProfile_ultrafast(Bc7EncSettings* settings);
        [DllImport(Lib)] public static extern void GetProfile_veryfast(Bc7EncSettings* settings);
        [DllImport(Lib)] public static extern void GetProfile_fast(Bc7EncSettings* settings);
        [DllImport(Lib)] public static extern void GetProfile_basic(Bc7EncSettings* settings);
        [DllImport(Lib)] public static extern void GetProfile_slow(Bc7EncSettings* settings);
        [DllImport(Lib)] public static extern void GetProfile_alpha_ultrafast(Bc7EncSettings* settings);
        [DllImport(Lib)] public static extern void GetProfile_alpha_veryfast(Bc7EncSettings* settings);
        [DllImport(Lib)] public static extern void GetProfile_alpha_fast(Bc7EncSettings* settings);
        [DllImport(Lib)] public static extern void GetProfile_alpha_basic(Bc7EncSettings* settings);
        [DllImport(Lib)] public static extern void GetProfile_alpha_slow(Bc7EncSettings* settings);
    }
}
