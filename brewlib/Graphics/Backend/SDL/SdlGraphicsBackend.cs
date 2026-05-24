namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using IO;
using Renderers;
using SDL3;
using Shaders;
using Textures;
using Util;

public sealed class SdlGraphicsBackend : IGraphicsBackend, IRendererFactory
{
    const int DefaultFrameTransferBufferPageSize = 4 * 1024 * 1024;
    const uint FrameUploadLatency = 3;
    readonly SdlGraphicsDevice device;
    readonly List<FrameTransferPage> frameTransferPages = new();

    readonly List<PendingBufferUpload> pendingBufferUploads = new(256);
    readonly nint window;
    bool framebufferHasContents, swapchainAcquireAttempted, disposed;
    Vector4 frameClearColor;

    nint swapchainTexture;
    uint swapchainWidth, swapchainHeight;

    public SdlGraphicsBackend(nint window = 0,
        bool debug = false,
        string preferredDriver = null)
    {
        this.window = window;

        var requestedFormats = SdlShaderCompiler.SupportedShaderFormats;

        var deviceHandle = SDL.CreateGPUDevice(requestedFormats, debug, preferredDriver);
        if (deviceHandle == nint.Zero)
            throw new InvalidOperationException($"Unable to create SDL GPU device: {SDL.GetError()}");

        device = new(this, deviceHandle);
        Device = device;
        Buffers = new SdlGraphicsBufferFactory(this);
        TransientBuffers = new SdlTransientGraphicsBufferFactory(this);
        RenderPipelines = new SdlRenderPipelineFactory(this);
        ShaderPrograms = new SdlShaderProgramFactory();
        TextureFactory = new SdlTextureFactory(this);

        var driver = SDL.GetGPUDeviceDriver(deviceHandle) ?? "unknown";
        var shaderFormats = SDL.GetGPUShaderFormats(deviceHandle);
        ShaderFormat = SdlShaderCompiler.SelectShaderFormat(shaderFormats);
        SDL.LogInfo(LogCategory.Render, $"SDL GPU driver: {driver}; shader formats: {shaderFormats}; selected: {ShaderFormat}");

        var fragmentSamplerCapacity = probeFragmentSamplerCapacity(deviceHandle,
            ShaderFormat,
            out var nativeNonUniformIndexing);

        var baselineSupported = fragmentSamplerCapacity > 0;
        SDL.LogInfo(LogCategory.Render,
            $"SDL GPU fragment samplers: {fragmentSamplerCapacity}; baseline probe: {baselineSupported}; non-uniform indexing: {nativeNonUniformIndexing}");

        var features = GraphicsBackendFeatures.TextureAtlases |
            GraphicsBackendFeatures.Instancing |
            GraphicsBackendFeatures.IndirectDraws;

        if (nativeNonUniformIndexing)
            features |= GraphicsBackendFeatures.NativeNonUniformTextureIndexing;

        Capabilities = new(features,
            16384,
            fragmentSamplerCapacity,
            0,
            0,
            fragmentSamplerCapacity,
            0);

        TextureUploader = new SdlAsyncTextureUploader(this);
    }

    internal nint DeviceHandle => device.DeviceHandle;
    internal nint CommandBuffer { get; private set; }

    internal nint RenderPass { get; private set; }

    internal uint FrameSerial { get; private set; }

    internal uint RenderPassSerial { get; private set; }

    internal SDL.GPUTextureFormat SwapchainFormat { get; private set; }
    internal SDL.GPUShaderFormat ShaderFormat { get; }
    internal uint SwapchainHeight => swapchainHeight;
    internal bool HasActiveFrame => CommandBuffer != nint.Zero;

    public string Name => "SDL GPU";
    public GraphicsBackendCapabilities Capabilities { get; }
    public ShaderSourceLanguage ShaderSourceLanguage => ShaderSourceLanguage.Hlsl;
    public IGraphicsDevice Device { get; }
    public IRendererFactory RendererFactory => this;
    public IGraphicsBufferFactory Buffers { get; }
    public ITransientGraphicsBufferFactory TransientBuffers { get; }
    public IRenderPipelineFactory RenderPipelines { get; }
    public IShaderAssetLoader ShaderAssets { get; } = new EmbeddedShaderAssetLoader();
    public IShaderProgramFactory ShaderPrograms { get; }
    public ITextureFactory TextureFactory { get; }
    public IAsyncTextureUploader TextureUploader { get; }
    public bool PrefersBufferedTransientDraws => true;

    public bool SupportsShaderExtension(string extensionName) => false;

    public void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        if (window != nint.Zero && !SDL.ClaimWindowForGPUDevice(DeviceHandle, window))
            throw new InvalidOperationException($"Unable to claim SDL window for GPU device: {SDL.GetError()}");

        if (window != nint.Zero)
            SwapchainFormat = SDL.GetGPUSwapchainTextureFormat(DeviceHandle, window);

        DrawState.Initialize(resourceContainer, textureContainer, this);
    }

    public bool BeginFrame(Vector4 clearColor)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (window == nint.Zero)
            throw new InvalidOperationException("SDL frame rendering requires a claimed SDL window");

        if (CommandBuffer != nint.Zero)
            throw new InvalidOperationException("An SDL GPU frame is already active");

        ResetFrameTransferPages();

        CommandBuffer = acquireCommandBuffer("frame");
        ++FrameSerial;

        frameClearColor = clearColor;
        framebufferHasContents = false;
        swapchainAcquireAttempted = false;
        swapchainTexture = nint.Zero;
        swapchainWidth = swapchainHeight = 0;
        return true;
    }

    public void EndFrame(bool discardFramebuffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (CommandBuffer == nint.Zero) return;

        if (!framebufferHasContents && RenderPass == nint.Zero && tryAcquireSwapchain())
            RequireRenderPass();

        EndRenderPass();
        submitActiveCommandBuffer();

        swapchainTexture = nint.Zero;
        swapchainWidth = swapchainHeight = 0;
        swapchainAcquireAttempted = false;
    }

    public void Dispose()
    {
        if (disposed) return;

        EndRenderPass();
        submitActiveCommandBuffer();
        WaitIdle();
        ReleaseFrameTransferPages();

        if (window != nint.Zero) SDL.ReleaseWindowFromGPUDevice(DeviceHandle, window);
        device.Dispose();

        disposed = true;
    }

    public IQuadRenderer CreateQuadRenderer()
        => new TexturedQuadRenderer(this);

    public ILineRenderer CreateLineRenderer()
        => new LineRenderer(this);

    static bool tryProbeFragmentShader(nint device,
        SDL.GPUShaderFormat shaderFormat,
        int samplerCount,
        bool useNonUniformIndexing)
    {
        var probeName = $"SdlGraphicsBackend.ProbeFragment[{samplerCount}, nu={useNonUniformIndexing}]";
        var hlsl = createFragmentProbeHlsl(samplerCount, useNonUniformIndexing);

        try
        {
            var shader = SdlShaderCompiler.CompileGraphicsShaderFromHlsl(device,
                shaderFormat,
                probeName,
                CompiledShaderStage.Fragment,
                hlsl);

            if (shader == nint.Zero)
            {
                SDL.LogInfo(LogCategory.Render, $"{probeName} SDL CreateGPUShader returned null: {SDL.GetError()}");
                return false;
            }

            SDL.ReleaseGPUShader(device, shader);
            return true;
        }
        catch (Exception ex)
        {
            SDL.LogInfo(LogCategory.Render, $"{probeName} SDL shader create failed: {ex.Message}");
            return false;
        }
    }

    static int probeFragmentSamplerCapacity(nint device,
        SDL.GPUShaderFormat shaderFormat,
        out bool nativeNonUniformIndexing)
    {
        nativeNonUniformIndexing = false;

        ReadOnlySpan<int> candidates = [128, 64, 32, 16, 8, 4, 1];
        foreach (var candidate in candidates)
        {
            if (!tryProbeFragmentShader(device, shaderFormat, candidate, false))
                continue;

            if (tryProbeFragmentShader(device, shaderFormat, candidate, true))
            {
                nativeNonUniformIndexing = true;
                return candidate;
            }

            return int.Min(candidate, 16);
        }

        return 1;
    }

    static string createFragmentProbeHlsl(int samplerCount, bool useNonUniformIndexing)
    {
        var source = $$"""
                       Texture2D<float4> u_textures[{{samplerCount}}] : register(t0, space2);
                       SamplerState u_samplers[{{samplerCount}}] : register(s0, space2);

                       struct FragmentInput
                       {
                           float4 Position : SV_Position;
                           float2 TextureCoord : TEXCOORD0;
                           nointerpolation int TextureSlot : TEXCOORD1;
                       };

                       float4 main(FragmentInput input) : SV_Target0
                       {
                       """;

        if (useNonUniformIndexing)
            return source + """
                                uint textureSlot = (uint)input.TextureSlot;
                                return u_textures[NonUniformResourceIndex(textureSlot)].Sample(u_samplers[NonUniformResourceIndex(textureSlot)], input.TextureCoord);
                            }
                            """;

        return source + $$"""
                              if (input.TextureSlot == {{samplerCount - 1}}) return u_textures[{{samplerCount - 1}}].Sample(u_samplers[{{samplerCount - 1}}], input.TextureCoord);
                              return u_textures[0].Sample(u_samplers[0], input.TextureCoord);
                          }
                          """;
    }

    bool tryAcquireSwapchain()
    {
        if (swapchainTexture != nint.Zero) return true;
        if (swapchainAcquireAttempted) return false;

        swapchainAcquireAttempted = true;

        if (!SDL.WaitAndAcquireGPUSwapchainTexture(CommandBuffer,
            window,
            out swapchainTexture,
            out swapchainWidth,
            out swapchainHeight))
            throw new InvalidOperationException($"Unable to acquire SDL GPU swapchain texture: {SDL.GetError()}");

        return swapchainTexture != nint.Zero;
    }

    internal void UploadBuffer(nint transferBuffer,
        nint buffer,
        uint sourceOffset,
        uint offset,
        uint size,
        bool cycle,
        string description)
    {
        if (transferBuffer == nint.Zero || buffer == nint.Zero || size == 0) return;

        var copyPass = beginCopyPass(description);
        var submitted = false;
        try
        {
            var source = new SDL.GPUTransferBufferLocation
            {
                TransferBuffer = transferBuffer,
                Offset = sourceOffset
            };

            var destination = new SDL.GPUBufferRegion
            {
                Buffer = buffer,
                Offset = offset,
                Size = size
            };

            SDL.UploadToGPUBuffer(copyPass.Handle, in source, in destination, cycle);

            completeCopyPass(copyPass);
            submitted = copyPass.Standalone;
        }
        catch
        {
            cancelCopyPass(copyPass, submitted);
            throw;
        }
    }

    internal FrameTransferUpload AllocateFrameTransfer(int sizeInBytes, string description)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (sizeInBytes < 0) throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, null);

        if (sizeInBytes == 0) return default;

        if (CommandBuffer == nint.Zero)
            throw new InvalidOperationException(
                $"SDL frame transfer allocation for {description} requires an active GPU command buffer");

        var page = getFrameTransferPage(sizeInBytes);
        var sourceOffset = align(page.Offset, 4);
        page.Offset = checked(sourceOffset + sizeInBytes);

        if (page.Mapped == nint.Zero)
        {
            page.Mapped = SDL.MapGPUTransferBuffer(DeviceHandle, page.Buffer, sourceOffset == 0);
            if (page.Mapped == nint.Zero)
                throw new InvalidOperationException(
                    $"Unable to map SDL frame transfer buffer for {description}: {SDL.GetError()}");
        }

        return new(page.Buffer, (uint)sourceOffset, page.Mapped + sourceOffset);
    }

    internal void QueueBufferUpload(nint transferBuffer,
        nint buffer,
        uint sourceOffset,
        uint offset,
        uint size,
        bool cycle,
        string description)
    {
        if (transferBuffer == nint.Zero || buffer == nint.Zero || size == 0) return;

        if (CommandBuffer == nint.Zero)
        {
            UploadBuffer(transferBuffer, buffer, sourceOffset, offset, size, cycle, description);
            return;
        }

        pendingBufferUploads.Add(new(transferBuffer, buffer, sourceOffset, offset, size, cycle));
    }

    internal void UploadTexture(nint transferBuffer,
        nint texture,
        int width,
        int height,
        int pixelsPerRow,
        int rowsPerLayer,
        int x,
        int y,
        bool generateMipmaps,
        string description)
    {
        if (transferBuffer == nint.Zero || texture == nint.Zero || width <= 0 || height <= 0) return;

        var copyPass = beginCopyPass(description);
        var submitted = false;
        try
        {
            var source = new SDL.GPUTextureTransferInfo
            {
                TransferBuffer = transferBuffer,
                PixelsPerRow = (uint)pixelsPerRow,
                RowsPerLayer = (uint)rowsPerLayer
            };

            var destination = new SDL.GPUTextureRegion
            {
                Texture = texture,
                W = (uint)width,
                H = (uint)height,
                D = 1,
                X = (uint)x,
                Y = (uint)y
            };

            SDL.UploadToGPUTexture(copyPass.Handle, in source, in destination, false);
            completeCopyPass(copyPass, generateMipmaps ? texture : nint.Zero);
            submitted = copyPass.Standalone;
        }
        catch
        {
            cancelCopyPass(copyPass, submitted);
            throw;
        }
    }

    internal void ReleaseBuffer(nint buffer)
    {
        if (buffer == nint.Zero || disposed) return;

        SDL.ReleaseGPUBuffer(DeviceHandle, buffer);
    }

    internal void ReleaseTransferBuffer(nint transferBuffer)
    {
        if (transferBuffer == nint.Zero || disposed) return;

        SDL.ReleaseGPUTransferBuffer(DeviceHandle, transferBuffer);
    }

    internal void ReleaseTexture(nint texture)
    {
        if (texture == nint.Zero || disposed) return;

        SDL.ReleaseGPUTexture(DeviceHandle, texture);
    }

    internal void ReleaseSampler(nint sampler)
    {
        if (sampler == nint.Zero || disposed) return;

        SDL.ReleaseGPUSampler(DeviceHandle, sampler);
    }

    internal void ReleaseShader(nint shader)
    {
        if (shader == nint.Zero || disposed) return;

        SDL.ReleaseGPUShader(DeviceHandle, shader);
    }

    internal void ReleaseGraphicsPipeline(nint pipeline)
    {
        if (pipeline == nint.Zero || disposed) return;

        SDL.ReleaseGPUGraphicsPipeline(DeviceHandle, pipeline);
    }

    internal bool IsFrameFenceSignaled(uint submittedFrameSerial)
        => unchecked(FrameSerial - submittedFrameSerial) >= FrameUploadLatency;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetReadyRenderPass(out nint renderPass)
    {
        renderPass = RenderPass;
        return renderPass != nint.Zero && pendingBufferUploads.Count == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal nint RequireRenderPass()
    {
        if (pendingBufferUploads.Count != 0) FlushPendingBufferUploads();
        if (RenderPass != nint.Zero) return RenderPass;

        if (CommandBuffer == nint.Zero)
            throw new InvalidOperationException("SDL render pass requested outside an active GPU command buffer");

        if (!tryAcquireSwapchain())
            return nint.Zero;

        Span<SDL.GPUColorTargetInfo> colorTargets = stackalloc SDL.GPUColorTargetInfo[1];
        colorTargets[0] = new()
        {
            Texture = swapchainTexture,
            ClearColor = new()
            {
                R = frameClearColor.X,
                G = frameClearColor.Y,
                B = frameClearColor.Z,
                A = frameClearColor.W
            },
            LoadOp = framebufferHasContents ? SDL.GPULoadOp.Load : SDL.GPULoadOp.Clear,
            StoreOp = SDL.GPUStoreOp.Store,
            Cycle = 0
        };

        RenderPass = SDL.BeginGPURenderPass(CommandBuffer, colorTargets.AsPointer(), 1, 0);
        if (RenderPass == nint.Zero)
            throw new InvalidOperationException($"Unable to begin SDL GPU render pass: {SDL.GetError()}");

        DrawState.CountDrawCall();

        ++RenderPassSerial;
        device.ApplyRenderPassState(RenderPass);
        return RenderPass;
    }

    internal void EndRenderPass()
    {
        if (RenderPass == nint.Zero) return;

        SDL.EndGPURenderPass(RenderPass);
        RenderPass = nint.Zero;
        framebufferHasContents = true;
    }

    internal void PrepareCopyFromDraw()
    {
        DrawState.FlushRenderer();
        EndRenderPass();
    }

    internal void FlushPendingBufferUploads()
    {
        if (pendingBufferUploads.Count == 0) return;

        if (CommandBuffer == nint.Zero)
            throw new InvalidOperationException("SDL queued buffer uploads require an active GPU command buffer");

        EndRenderPass();
        UnmapFrameTransferPages();

        var copyPass = SDL.BeginGPUCopyPass(CommandBuffer);
        if (copyPass == nint.Zero)
            throw new InvalidOperationException($"Unable to begin SDL GPU copy pass for queued buffer uploads: {SDL.GetError()}");

        foreach (var upload in pendingBufferUploads)
        {
            var source = new SDL.GPUTransferBufferLocation
            {
                TransferBuffer = upload.TransferBuffer,
                Offset = upload.SourceOffset
            };

            var destination = new SDL.GPUBufferRegion
            {
                Buffer = upload.Buffer,
                Offset = upload.Offset,
                Size = upload.Size
            };

            SDL.UploadToGPUBuffer(copyPass, in source, in destination, upload.Cycle);
        }

        SDL.EndGPUCopyPass(copyPass);
        pendingBufferUploads.Clear();
    }

    CopyPass beginCopyPass(string description)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var standalone = CommandBuffer == nint.Zero;
        var copyCommandBuffer = standalone ? acquireCommandBuffer(description) : CommandBuffer;

        try
        {
            if (!standalone)
            {
                if (pendingBufferUploads.Count != 0) FlushPendingBufferUploads();
                EndRenderPass();
            }

            var copyPass = SDL.BeginGPUCopyPass(copyCommandBuffer);
            if (copyPass == nint.Zero)
                throw new InvalidOperationException($"Unable to begin SDL GPU copy pass for {description}: {SDL.GetError()}");

            return new(copyCommandBuffer, copyPass, standalone);
        }
        catch
        {
            if (standalone)
                SDL.CancelGPUCommandBuffer(copyCommandBuffer);

            throw;
        }
    }

    void completeCopyPass(CopyPass copyPass, nint textureToGenerateMipmapsFor = 0)
    {
        SDL.EndGPUCopyPass(copyPass.Handle);

        if (textureToGenerateMipmapsFor != nint.Zero)
            SDL.GenerateMipmapsForGPUTexture(copyPass.CommandBuffer, textureToGenerateMipmapsFor);

        if (copyPass.Standalone)
            submitStandaloneCommandBuffer(copyPass.CommandBuffer);
    }

    static void cancelCopyPass(CopyPass copyPass, bool submitted)
    {
        if (copyPass.Standalone && !submitted)
            SDL.CancelGPUCommandBuffer(copyPass.CommandBuffer);
    }

    void submitActiveCommandBuffer()
    {
        if (CommandBuffer == nint.Zero) return;

        EndRenderPass();
        if (pendingBufferUploads.Count != 0) FlushPendingBufferUploads();
        UnmapFrameTransferPages();

        var submittedCommandBuffer = CommandBuffer;
        CommandBuffer = nint.Zero;

        if (!SDL.SubmitGPUCommandBuffer(submittedCommandBuffer))
            throw new InvalidOperationException($"Unable to submit SDL GPU command buffer: {SDL.GetError()}");
    }

    static void submitStandaloneCommandBuffer(nint submittedCommandBuffer)
    {
        if (!SDL.SubmitGPUCommandBuffer(submittedCommandBuffer))
            throw new InvalidOperationException($"Unable to submit SDL GPU command buffer: {SDL.GetError()}");
    }

    void WaitIdle()
    {
        if (!SDL.WaitForGPUIdle(DeviceHandle))
            throw new InvalidOperationException($"Unable to wait for SDL GPU idle: {SDL.GetError()}");
    }

    nint acquireCommandBuffer(string description)
    {
        var acquiredCommandBuffer = SDL.AcquireGPUCommandBuffer(DeviceHandle);
        if (acquiredCommandBuffer == nint.Zero)
            throw new InvalidOperationException($"Unable to acquire SDL GPU command buffer for {description}: {SDL.GetError()}");

        return acquiredCommandBuffer;
    }

    FrameTransferPage getFrameTransferPage(int sizeInBytes)
    {
        foreach (var candidate in frameTransferPages)
        {
            var offset = align(candidate.Offset, 4);
            if (sizeInBytes <= candidate.Capacity - offset)
                return candidate;
        }

        var capacity = DefaultFrameTransferBufferPageSize;
        while (capacity < sizeInBytes)
            capacity = checked(capacity * 2);

        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)capacity
        };

        var transferBuffer = SDL.CreateGPUTransferBuffer(DeviceHandle, in transferCreateInfo);
        if (transferBuffer == nint.Zero)
            throw new InvalidOperationException($"Unable to create SDL frame transfer buffer: {SDL.GetError()}");

        var createdPage = new FrameTransferPage(transferBuffer, capacity);
        frameTransferPages.Add(createdPage);
        return createdPage;
    }

    void ResetFrameTransferPages()
    {
        UnmapFrameTransferPages();

        foreach (var page in frameTransferPages)
            page.Offset = 0;
    }

    void UnmapFrameTransferPages()
    {
        foreach (var page in frameTransferPages)
        {
            if (page.Mapped == nint.Zero) continue;

            SDL.UnmapGPUTransferBuffer(DeviceHandle, page.Buffer);
            page.Mapped = nint.Zero;
        }
    }

    void ReleaseFrameTransferPages()
    {
        UnmapFrameTransferPages();

        foreach (var page in frameTransferPages)
            ReleaseTransferBuffer(page.Buffer);

        frameTransferPages.Clear();
    }

    static int align(int value, int alignment)
        => value + alignment - 1 & ~(alignment - 1);

    readonly record struct CopyPass(nint CommandBuffer, nint Handle, bool Standalone);

    internal readonly record struct FrameTransferUpload(nint TransferBuffer, uint SourceOffset, nint Data);

    sealed class FrameTransferPage(nint buffer, int capacity)
    {
        public readonly nint Buffer = buffer;
        public readonly int Capacity = capacity;
        public nint Mapped;
        public int Offset;
    }

    readonly record struct PendingBufferUpload(
        nint TransferBuffer,
        nint Buffer,
        uint SourceOffset,
        uint Offset,
        uint Size,
        bool Cycle);
}