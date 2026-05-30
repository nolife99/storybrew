namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using WgpuBuffer = Ahjo.Wgpu.Buffer;
using WgpuTexture = Ahjo.Wgpu.Texture;

/// <summary>
///     GPU block-compression encoder (BC7 + BC1) built on the vendored block_compression compute shaders. It moves the
///     encode off the CPU entirely: instead of compressing on a worker thread and uploading blocks, it uploads the
///     RGBA to a temporary source texture, dispatches the compress kernel into a storage buffer, and copies that buffer
///     into the block-aligned BC texture — eliminating the per-texture managed-encoder allocations and CPU time.
///
///     Device-thread only for encoding: wgpu command submission is single-threaded, so every <c>EncodeInto</c> happens
///     on the device thread (the synchronous load path, or the async uploader's main-thread Upload step — the dispatch
///     only records commands, so it doesn't block). Pipeline compilation, however, runs once on a background thread via
///     <c>BeginPrewarm</c> (called at startup when GPU compression is enabled): BC7 pipeline creation is slow under
///     DX12 (DXC), and compiling it off the device thread keeps it from stalling the first scene load. If an encode
///     arrives before the compile has finished, it blocks until ready.
///
///     Output alignment: the shaders store blocks tightly, but <c>CopyBufferToTexture</c> needs a 256-byte row stride.
///     The vendored shaders take a <c>blocks_per_row</c> uniform for the store stride (see <see cref="WebGpuBcShaders"/>),
///     so the output buffer's rows are padded to 256 while the BC texture stays at 4-pixel block alignment; the padding
///     is transient (the buffer is freed after the copy) and never resident in the texture.
/// </summary>
sealed unsafe class WebGpuBcComputeEncoder : IDisposable
{
    const int Bc7BlockBytes = 16;
    const int Bc1BlockBytes = 8;

    // BC7 "alpha_basic" profile, in the shader's Settings field order:
    // refine_iterations[8], mode_selection[4], skip_mode2, fast_skip_threshold_mode1/3/7,
    // mode45_channel0, refine_iterations_channel, channels.
    static readonly uint[] Bc7AlphaBasicSettings =
    [
        2, 2, 2, 2, 2, 2, 2, 2,
        1, 1, 1, 1,
        1,
        12, 8, 8,
        0,
        2,
        4
    ];

    readonly WebGpuDeviceContext deviceContext;
    readonly object initGate = new();

    Task prewarmTask;       // background pipeline compilation (started by BeginPrewarm, or lazily on first encode)
    bool pipelinesReady;    // set under initGate once modules/layouts/pipelines/settings buffer exist
    bool fullyInitialized;  // device-thread only: settings uploaded, ready to encode
    bool disposed;

    ShaderModule bc7Module, bc1Module;
    BindGroupLayout bc7Layout, bc1Layout;
    PipelineLayout bc7PipelineLayout, bc1PipelineLayout;
    ComputePipeline bc7Pipeline, bc1Pipeline;
    WgpuBuffer bc7SettingsBuffer;

    // Reused encode scratch (device-thread only): one source texture + output buffer + uniform + bind groups, grown
    // only to the largest encode seen. The previous per-encode create+destroy churned differently-sized device-local
    // allocations that gpu-alloc kept on its free-list as an unbound ~400 MiB heap; reuse keeps one allocation each.
    WgpuTexture scratchSource;
    TextureView scratchSourceView;
    int scratchWidth, scratchHeight;
    WgpuBuffer scratchOutput;
    ulong scratchOutputCapacity;
    WgpuBuffer scratchUniform;
    BindGroup bc7BindGroup, bc1BindGroup;
    bool scratchBindGroupsValid;

    [ThreadStatic] static byte[] padScratch;

    public WebGpuBcComputeEncoder(WebGpuDeviceContext deviceContext)
        => this.deviceContext = deviceContext ?? throw new ArgumentNullException(nameof(deviceContext));

    /// <summary>
    ///     Starts compute-pipeline compilation on a background thread (idempotent). Call once at startup when GPU
    ///     compression is enabled so the slow DX12 BC7 pipeline build finishes — or is at least in flight — before the
    ///     first scene loads, instead of stalling it. wgpu resource creation is thread-safe and no queue work happens
    ///     here, so this is safe to run off the device thread.
    /// </summary>
    public void BeginPrewarm()
    {
        lock (initGate)
        {
            if (disposed || pipelinesReady || prewarmTask is not null) return;
            prewarmTask = Task.Factory.StartNew(CompilePipelines, TaskCreationOptions.LongRunning);
        }
    }

    // Heavy part, off the device thread. Runs on a dedicated LongRunning thread (NOT Task.Run / the thread pool):
    // during a scene load the pool is saturated with texture-decode work, and a pool-queued compile would wait behind
    // all of it for minutes while decoded bitmaps pile up. The two shaders compile in parallel — BC7 (the slow one
    // under DX12) on this thread, BC1 alongside on a second dedicated thread. No queue submission here; the one-time
    // settings upload happens on the device thread in EnsureInitialized.
    void CompilePipelines()
    {
        var bc1Thread = new Thread(CompileBc1) { IsBackground = true, Name = "wgpu BC1 pipeline compile" };
        bc1Thread.Start();

        CompileBc7();
        bc1Thread.Join();

        // Settings storage buffer for the alpha_basic profile (no compilation; written once, on the device thread).
        var settings = deviceContext.Device.CreateBuffer(new BufferDescriptor
        {
            Size = (ulong)AlignUp(Bc7AlphaBasicSettings.Length * sizeof(uint), 256),
            Usage = BufferUsage.Storage | BufferUsage.CopyDst,
            MappedAtCreation = false
        });

        lock (initGate)
        {
            bc7SettingsBuffer = settings;
            pipelinesReady = true;
        }
    }

    // group 0: source texture, output storage buffer, uniforms, read-only settings storage buffer.
    void CompileBc7()
    {
        var device = deviceContext.Device;

        var module = CompileWgsl(WebGpuBcShaders.Bc7);
        var layout = device.CreateBindGroupLayout(
        [
            BindGroupLayoutEntry.Texture(0, ShaderStage.Compute),
            BindGroupLayoutEntry.StorageBuffer(1, ShaderStage.Compute, false, false, 0),
            BindGroupLayoutEntry.Buffer(2, ShaderStage.Compute, WGPUBufferBindingType.Uniform, false, 0),
            BindGroupLayoutEntry.StorageBuffer(3, ShaderStage.Compute, true, false, 0)
        ]);
        var pipelineLayout = device.CreatePipelineLayout([layout], 0);
        var pipeline = device.CreateComputePipeline(module, "compress_bc7"u8, pipelineLayout);

        bc7Module = module;
        bc7Layout = layout;
        bc7PipelineLayout = pipelineLayout;
        bc7Pipeline = pipeline;
    }

    // group 0: source texture, output storage buffer, uniforms (BC1 has no settings buffer).
    void CompileBc1()
    {
        var device = deviceContext.Device;

        var module = CompileWgsl(WebGpuBcShaders.Bc1To5);
        var layout = device.CreateBindGroupLayout(
        [
            BindGroupLayoutEntry.Texture(0, ShaderStage.Compute),
            BindGroupLayoutEntry.StorageBuffer(1, ShaderStage.Compute, false, false, 0),
            BindGroupLayoutEntry.Buffer(2, ShaderStage.Compute, WGPUBufferBindingType.Uniform, false, 0)
        ]);
        var pipelineLayout = device.CreatePipelineLayout([layout], 0);
        var pipeline = device.CreateComputePipeline(module, "compress_bc1"u8, pipelineLayout);

        bc1Module = module;
        bc1Layout = layout;
        bc1PipelineLayout = pipelineLayout;
        bc1Pipeline = pipeline;
    }

    // Device-thread only. Blocks until the background compile has finished (compiling synchronously on a worker here
    // if prewarm was never started), then uploads the static settings once. The wait is the "loaded a scene before
    // it was ready" path; once warm this is a couple of field reads.
    void EnsureInitialized()
    {
        if (fullyInitialized) return;

        Task toWait;
        lock (initGate)
        {
            prewarmTask ??= Task.Factory.StartNew(CompilePipelines, TaskCreationOptions.LongRunning);
            toWait = prewarmTask;
        }

        toWait.Wait();

        deviceContext.Queue.WriteBuffer<uint>(bc7SettingsBuffer, 0, Bc7AlphaBasicSettings);
        fullyInitialized = true;
    }

    /// <summary>
    ///     Encodes <paramref name="rgba"/> (the content sub-rect, <paramref name="srcStride"/> = source row pitch in
    ///     bytes) into <paramref name="bcTexture"/>, a freshly-created block-aligned BC texture of
    ///     <paramref name="format"/>. Device-thread only.
    /// </summary>
    public void EncodeInto(WebGpuTexture bcTexture,
        scoped ReadOnlySpan<byte> rgba,
        int contentWidth,
        int contentHeight,
        int srcStride,
        Bc7Encoder.BcFormat format)
    {
        EnsureInitialized();

        var isBc7 = format == Bc7Encoder.BcFormat.Bc7;
        var physWidth = bcTexture.Width;   // already block-aligned by the factory
        var physHeight = bcTexture.Height;

        var blockBytes = isBc7 ? Bc7BlockBytes : Bc1BlockBytes;
        var blocksWide = (physWidth + 3) / 4;
        var blocksHigh = (physHeight + 3) / 4;

        var alignedRowBytes = AlignUp(blocksWide * blockBytes, 256);
        var paddedBlocksPerRow = alignedRowBytes / blockBytes;
        var outputSize = (ulong)alignedRowBytes * (ulong)blocksHigh;

        var device = deviceContext.Device;

        // Reuse one persistent source texture / output buffer / uniform / bind group, growing only to the largest
        // encode seen. The previous per-encode create+destroy churned differently-sized device-local allocations that
        // gpu-alloc retained on its free-list as an unbound ~400 MiB heap; reuse keeps one allocation each, no churn.
        EnsureScratch(physWidth, physHeight, outputSize);

        // 1. Upload the edge-padded RGBA into the scratch source through the bounded stager page pool (the same path
        //    regular RGBA textures take) rather than Queue.WriteTexture. The shader samples only [0,physWidth) x
        //    [0,physHeight); any stale region beyond that (from a larger prior encode) is never read.
        UploadSource(rgba, contentWidth, contentHeight, srcStride, physWidth, physHeight);

        // 2. Uniforms for this encode's actual dimensions (the scratch may be larger than physWidth x physHeight).
        Span<uint> uniforms = stackalloc uint[8];
        uniforms[0] = (uint)physWidth;
        uniforms[1] = (uint)physHeight;
        uniforms[2] = 0;                          // texture_y_offset
        uniforms[3] = 0;                          // blocks_offset (in u32 elements)
        uniforms[4] = (uint)paddedBlocksPerRow;   // output row stride in blocks (256-byte aligned)
        deviceContext.Queue.WriteBuffer<uint>(scratchUniform, 0, uniforms);

        // 3. Dispatch the compress kernel (one block per thread, 8x8 workgroups) for this encode's blocks, then copy
        //    the packed blocks into the BC texture. The source upload and WriteBuffer are ordered before this submit,
        //    so the source and uniforms are populated before the dispatch reads them. The cached bind group already
        //    references the scratch source view / output / uniform.
        var bindGroup = isBc7 ? bc7BindGroup : bc1BindGroup;
        var workgroupsX = (uint)((blocksWide + 7) / 8);
        var workgroupsY = (uint)((blocksHigh + 7) / 8);

        using var encoder = device.CreateCommandEncoder();
        using (var pass = encoder.BeginComputePass())
        {
            pass.SetPipeline(isBc7 ? bc7Pipeline : bc1Pipeline);
            pass.SetBindGroup(0, bindGroup);
            pass.DispatchWorkgroups(workgroupsX, workgroupsY, 1);
            pass.End();
        }

        var extent = new WGPUExtent3D { width = (uint)physWidth, height = (uint)physHeight, depthOrArrayLayers = 1 };
        encoder.CopyBufferToTexture(scratchOutput, (uint)alignedRowBytes, (uint)blocksHigh, bcTexture.WgpuTexture, in extent);

        using (var cmd = encoder.Finish())
            deviceContext.Queue.Submit(cmd);

        // No per-encode teardown: the scratch resources persist and are reused by the next encode.
    }

    // Device-thread only. Ensures the reused source texture / output buffer / uniform / bind groups exist and are at
    // least large enough for this encode, growing (and rebuilding the dependent bind groups) only when a larger
    // texture than any seen so far arrives. Disposing the prior handles here is safe: wgpu defers the GPU-side free of
    // a destroyed/released resource until the submissions still referencing it complete.
    void EnsureScratch(int physWidth, int physHeight, ulong requiredOutputBytes)
    {
        var device = deviceContext.Device;

        if (scratchSource.IsNull || scratchWidth < physWidth || scratchHeight < physHeight)
        {
            var newWidth = Math.Max(scratchWidth, physWidth);
            var newHeight = Math.Max(scratchHeight, physHeight);

            if (!scratchSourceView.IsNull) scratchSourceView.Dispose();
            DestroyTexture(scratchSource);

            scratchSource = CreateSourceTexture(newWidth, newHeight);
            scratchSourceView = scratchSource.CreateView(SourceViewDescriptor);
            scratchWidth = newWidth;
            scratchHeight = newHeight;
            scratchBindGroupsValid = false;
        }

        if (scratchOutput.IsNull || scratchOutputCapacity < requiredOutputBytes)
        {
            var newCapacity = Math.Max(scratchOutputCapacity, requiredOutputBytes);

            DestroyBuffer(scratchOutput);
            scratchOutput = device.CreateBuffer(new BufferDescriptor
            {
                Size = newCapacity,
                Usage = BufferUsage.Storage | BufferUsage.CopySrc,
                MappedAtCreation = false
            });
            scratchOutputCapacity = newCapacity;
            scratchBindGroupsValid = false;
        }

        if (scratchUniform.IsNull)
            scratchUniform = device.CreateBuffer(new BufferDescriptor
            {
                Size = 32,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
                MappedAtCreation = false
            });

        if (scratchBindGroupsValid) return;

        // The output buffer is bound whole (size 0 == entire buffer); the shader writes only this encode's region as
        // determined by paddedBlocksPerRow/blocksHigh, so a larger reused output buffer is fine.
        if (!bc7BindGroup.IsNull) bc7BindGroup.Dispose();
        if (!bc1BindGroup.IsNull) bc1BindGroup.Dispose();

        bc7BindGroup = device.CreateBindGroup(bc7Layout,
        [
            BindGroupEntry.TextureView(0, scratchSourceView),
            BindGroupEntry.Buffer(1, scratchOutput),
            BindGroupEntry.Buffer(2, scratchUniform),
            BindGroupEntry.Buffer(3, bc7SettingsBuffer)
        ]);

        bc1BindGroup = device.CreateBindGroup(bc1Layout,
        [
            BindGroupEntry.TextureView(0, scratchSourceView),
            BindGroupEntry.Buffer(1, scratchOutput),
            BindGroupEntry.Buffer(2, scratchUniform)
        ]);

        scratchBindGroupsValid = true;
    }

    ShaderModule CompileWgsl(string source)
    {
        var utf8 = Encoding.UTF8.GetBytes(source);
        var descriptor = new ShaderModuleDescriptor { Source = ShaderSource.FromWgsl(utf8) };
        return deviceContext.Device.CreateShaderModule(in descriptor);
    }

    WgpuTexture CreateSourceTexture(int width, int height)
    {
        var descriptor = new TextureDescriptor
        {
            Size = new()
            {
                width = (uint)width,
                height = (uint)height,
                depthOrArrayLayers = 1
            },
            Format = WGPUTextureFormat.RGBA8Unorm,
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
            Dimension = WGPUTextureDimension._2D,
            MipLevelCount = 1,
            SampleCount = 1
        };

        return deviceContext.Device.CreateTexture(in descriptor);
    }

    static TextureViewDescriptor SourceViewDescriptor => new()
    {
        Format = WGPUTextureFormat.RGBA8Unorm,
        Dimension = WGPUTextureViewDimension._2D,
        BaseMipLevel = 0,
        MipLevelCount = 1,
        BaseArrayLayer = 0,
        ArrayLayerCount = 1,
        Aspect = WGPUTextureAspect.All,
        Usage = TextureUsage.TextureBinding
    };

    void UploadSource(scoped ReadOnlySpan<byte> rgba,
        int contentWidth,
        int contentHeight,
        int srcStride,
        int physWidth,
        int physHeight)
    {
        var dstStride = physWidth * 4;
        var needed = dstStride * physHeight;
        if (padScratch is null || padScratch.Length < needed)
            padScratch = GC.AllocateUninitializedArray<byte>(needed);

        var contentRowBytes = contentWidth * 4;
        for (var y = 0; y < physHeight; ++y)
        {
            var srcY = y < contentHeight ? y : contentHeight - 1;
            var srcRow = rgba.Slice(srcY * srcStride, contentRowBytes);
            var dstRow = padScratch.AsSpan(y * dstStride, dstStride);

            srcRow.CopyTo(dstRow);

            if (physWidth > contentWidth)
            {
                var last = MemoryMarshal.Read<uint>(srcRow.Slice((contentWidth - 1) * 4, 4));
                MemoryMarshal.Cast<byte, uint>(dstRow[contentRowBytes..]).Fill(last);
            }
        }

        // Route the source upload through the shared stager page pool (the same bounded path regular RGBA uploads use)
        // instead of Queue.WriteTexture, whose internal staging is not reclaimed until the next submit. The full
        // block-aligned region is written so the sampled area is fully defined even when the scratch is larger.
        deviceContext.TextureStager.UploadRawToTexture(scratchSource, padScratch.AsSpan(0, needed), physWidth, physHeight, 0, 0, dstStride);
    }

    static void DestroyBuffer(WgpuBuffer buffer)
    {
        if (buffer.IsNull) return;
        WGPU.wgpuBufferDestroy(buffer.Handle);
        buffer.Dispose();
    }

    static void DestroyTexture(WgpuTexture texture)
    {
        if (texture.IsNull) return;
        WGPU.wgpuTextureDestroy(texture.Handle);
        texture.Dispose();
    }

    static int AlignUp(int value, int alignment) => (value + alignment - 1) & ~(alignment - 1);

    public void Dispose()
    {
        Task toWait;
        lock (initGate)
        {
            if (disposed) return;
            disposed = true;
            toWait = prewarmTask;
        }

        // Don't release handles out from under an in-flight background compile.
        try { toWait?.Wait(); }
        catch { /* a faulted prewarm produced nothing to dispose */ }

        // Reused encode scratch (all null if no encode ever ran; the Destroy/IsNull guards handle that).
        if (!bc7BindGroup.IsNull) bc7BindGroup.Dispose();
        if (!bc1BindGroup.IsNull) bc1BindGroup.Dispose();
        if (!scratchSourceView.IsNull) scratchSourceView.Dispose();
        DestroyTexture(scratchSource);
        DestroyBuffer(scratchOutput);
        DestroyBuffer(scratchUniform);

        if (!pipelinesReady) return;

        bc7Pipeline.Dispose();
        bc1Pipeline.Dispose();
        bc7PipelineLayout.Dispose();
        bc1PipelineLayout.Dispose();
        bc7Layout.Dispose();
        bc1Layout.Dispose();
        bc7Module.Dispose();
        bc1Module.Dispose();
        DestroyBuffer(bc7SettingsBuffer);
    }
}
