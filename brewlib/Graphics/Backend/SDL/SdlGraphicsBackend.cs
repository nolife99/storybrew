namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Collections.Generic;
using System.Numerics;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using BrewLib.IO;
using BrewLib.Util;
using SDL3;

public sealed class SdlGraphicsBackend : IGraphicsBackend, IRendererFactory
{
    readonly SdlShaderCrossContext shaderCrossContext;
    readonly nint window;
    readonly List<SubmittedWork> submittedWork = [];
    readonly Stack<ResourceReleaseBatch> releaseBatchPool = [];

    ResourceReleaseBatch activeCommandReleases;
    List<SdlUploadFence> activeUploadFences;
    nint commandBuffer, renderPass, swapchainTexture;
    uint swapchainWidth, swapchainHeight;
    uint frameSerial, renderPassSerial;
    Vector4 frameClearColor;
    bool framebufferHasContents, disposed;

    public SdlGraphicsBackend(nint window = 0,
        bool debug = false,
        string preferredDriver = null)
    {
        shaderCrossContext = new();
        this.window = window;

        var requestedFormats = SdlShaderCrossContext.SupportedHlslShaderFormats |
            SdlShaderCrossContext.SupportedSpirVShaderFormats |
            SDL.GPUShaderFormat.SPIRV;

        var deviceHandle = SDL.CreateGPUDevice(requestedFormats, debug, preferredDriver);
        if (deviceHandle == nint.Zero)
            throw new InvalidOperationException($"Unable to create SDL GPU device: {SDL.GetError()}");

        var device = new SdlGraphicsDevice(this, deviceHandle);
        Device = device;
        Buffers = new SdlGraphicsBufferFactory(this);
        TransientBuffers = new SdlTransientGraphicsBufferFactory(this);
        RenderPipelines = new SdlRenderPipelineFactory(this);
        ShaderPrograms = new SdlShaderProgramFactory();
        TextureFactory = new SdlTextureFactory(this);

        var driver = SDL.GetGPUDeviceDriver(deviceHandle) ?? "unknown";
        var shaderFormats = SDL.GetGPUShaderFormats(deviceHandle);
        SDL.LogInfo(LogCategory.Render, $"SDL GPU driver: {driver}; shader formats: {shaderFormats}");

        Capabilities = new(GraphicsBackendFeatures.TextureAtlases | GraphicsBackendFeatures.Instancing,
            16384,
            16,
            0,
            0,
            16,
            0);
        TextureUploader = new SdlAsyncTextureUploader(this);
    }

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

    internal nint DeviceHandle => ((SdlGraphicsDevice)Device).DeviceHandle;
    internal nint CommandBuffer => commandBuffer;
    internal nint RenderPass => renderPass;
    internal uint FrameSerial => frameSerial;
    internal uint RenderPassSerial => renderPassSerial;
    internal SDL.GPUTextureFormat SwapchainFormat { get; private set; }
    internal uint SwapchainHeight => swapchainHeight;
    internal bool HasActiveFrame => commandBuffer != nint.Zero && swapchainTexture != nint.Zero;

    public bool SupportsShaderExtension(string extensionName) => false;

    public void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        if (window != nint.Zero && !SDL.ClaimWindowForGPUDevice(DeviceHandle, window))
            throw new InvalidOperationException($"Unable to claim SDL window for GPU device: {SDL.GetError()}");

        if (window != nint.Zero)
            SwapchainFormat = SDL.GetGPUSwapchainTextureFormat(DeviceHandle, window);

        DrawState.Initialize(resourceContainer, textureContainer, this);
    }

    public IQuadRenderer CreateQuadRenderer()
        => new TexturedQuadRenderer(this);

    public ILineRenderer CreateLineRenderer()
        => new LineRenderer(this);

    public bool BeginFrame(Vector4 clearColor)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (window == nint.Zero)
            throw new InvalidOperationException("SDL frame rendering requires a claimed SDL window");
        if (commandBuffer != nint.Zero)
            throw new InvalidOperationException("An SDL GPU frame is already active");

        ReleaseCompletedSubmissions();

        commandBuffer = acquireCommandBuffer("frame");
        ++frameSerial;

        if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer,
                window,
                out swapchainTexture,
                out swapchainWidth,
                out swapchainHeight))
            throw new InvalidOperationException($"Unable to acquire SDL GPU swapchain texture: {SDL.GetError()}");

        if (swapchainTexture == nint.Zero)
        {
            submitActiveCommandBuffer();
            return false;
        }

        frameClearColor = clearColor;
        framebufferHasContents = false;
        return true;
    }

    public void EndFrame(bool discardFramebuffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (commandBuffer == nint.Zero) return;

        if (swapchainTexture != nint.Zero && renderPass == nint.Zero && !framebufferHasContents)
            RequireRenderPass();

        EndRenderPass();
        submitActiveCommandBuffer();

        swapchainTexture = nint.Zero;
        swapchainWidth = swapchainHeight = 0;
        ReleaseCompletedSubmissions();
    }

    public void Dispose()
    {
        if (disposed) return;

        EndRenderPass();
        submitActiveCommandBuffer();
        WaitIdleAndReleaseSubmittedWork();

        if (window != nint.Zero) SDL.ReleaseWindowFromGPUDevice(DeviceHandle, window);
        Device.Dispose();
        shaderCrossContext.Dispose();

        disposed = true;
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
            var source = new SDL.GPUTransferBufferLocation { TransferBuffer = transferBuffer };
            source.Offset = sourceOffset;
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

    internal void UploadTexture(nint transferBuffer,
        nint texture,
        int width,
        int height,
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
                PixelsPerRow = (uint)width,
                RowsPerLayer = (uint)height
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
        => releaseOrRetire(buffer, ResourceKind.Buffer);

    internal void ReleaseTransferBuffer(nint transferBuffer)
        => releaseOrRetire(transferBuffer, ResourceKind.TransferBuffer);

    internal void ReleaseTexture(nint texture)
        => releaseOrRetire(texture, ResourceKind.Texture);

    internal void ReleaseSampler(nint sampler)
        => releaseOrRetire(sampler, ResourceKind.Sampler);

    internal void ReleaseShader(nint shader)
        => releaseOrRetire(shader, ResourceKind.Shader);

    internal void ReleaseGraphicsPipeline(nint pipeline)
        => releaseOrRetire(pipeline, ResourceKind.GraphicsPipeline);

    internal IGpuUploadFence CreateUploadFence()
    {
        var fence = new SdlUploadFence(this);
        if (commandBuffer == nint.Zero)
        {
            fence.MarkCompleted();
            return fence;
        }

        (activeUploadFences ??= []).Add(fence);
        return fence;
    }

    internal nint RequireRenderPass()
    {
        if (renderPass != nint.Zero) return renderPass;
        if (commandBuffer == nint.Zero)
            throw new InvalidOperationException("SDL render pass requested outside an active GPU command buffer");
        if (swapchainTexture == nint.Zero)
            throw new InvalidOperationException("SDL render pass requested without an acquired swapchain texture");

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

        renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargets.AsPointer(), 1, 0);
        if (renderPass == nint.Zero)
            throw new InvalidOperationException($"Unable to begin SDL GPU render pass: {SDL.GetError()}");

        ++renderPassSerial;
        ((SdlGraphicsDevice)Device).ApplyRenderPassState(renderPass);
        return renderPass;
    }

    internal void EndRenderPass()
    {
        if (renderPass == nint.Zero) return;

        SDL.EndGPURenderPass(renderPass);
        renderPass = nint.Zero;
        framebufferHasContents = true;
    }

    internal void PrepareCopyFromDraw()
    {
        DrawState.FlushRenderer();
        EndRenderPass();
    }

    CopyPass beginCopyPass(string description)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var standalone = commandBuffer == nint.Zero;
        var copyCommandBuffer = standalone ? acquireCommandBuffer(description) : commandBuffer;

        try
        {
            if (!standalone)
                EndRenderPass();

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
            submitStandaloneCommandBuffer(copyPass.CommandBuffer, null);
    }

    static void cancelCopyPass(CopyPass copyPass, bool submitted)
    {
        if (copyPass.Standalone && !submitted)
            SDL.CancelGPUCommandBuffer(copyPass.CommandBuffer);
    }

    void releaseOrRetire(nint handle, ResourceKind kind)
    {
        if (handle == nint.Zero || disposed) return;

        if (commandBuffer != nint.Zero)
        {
            (activeCommandReleases ??= rentReleaseBatch()).Add(kind, handle);
            return;
        }

        if (submittedWork.Count != 0)
        {
            var index = submittedWork.Count - 1;
            var submission = submittedWork[index];
            (submission.Resources ??= rentReleaseBatch()).Add(kind, handle);
            submittedWork[index] = submission;
            return;
        }

        releaseResource(DeviceHandle, kind, handle);
    }

    void submitActiveCommandBuffer()
    {
        if (commandBuffer == nint.Zero) return;

        var submittedCommandBuffer = commandBuffer;
        commandBuffer = nint.Zero;

        submitCommandBufferWithFence(submittedCommandBuffer, activeCommandReleases, activeUploadFences);
        activeCommandReleases = null;
        activeUploadFences = null;
    }

    void submitStandaloneCommandBuffer(nint submittedCommandBuffer, ResourceReleaseBatch resources)
    {
        submitCommandBufferWithFence(submittedCommandBuffer, resources, null);
        ReleaseCompletedSubmissions();
    }

    void submitCommandBufferWithFence(nint submittedCommandBuffer,
        ResourceReleaseBatch resources,
        List<SdlUploadFence> uploadFences)
    {
        var fence = SDL.SubmitGPUCommandBufferAndAcquireFence(submittedCommandBuffer);
        if (fence == nint.Zero)
        {
            releaseAndReturn(resources);
            throw new InvalidOperationException($"Unable to submit SDL GPU command buffer: {SDL.GetError()}");
        }

        if (uploadFences is not null)
            for (var i = 0; i < uploadFences.Count; ++i)
                uploadFences[i].Attach(fence);

        submittedWork.Add(new(fence, resources, uploadFences));
    }

    void ReleaseCompletedSubmissions()
    {
        for (var i = submittedWork.Count - 1; i >= 0; --i)
        {
            var submission = submittedWork[i];
            if (!SDL.QueryGPUFence(DeviceHandle, submission.Fence)) continue;

            submission.MarkUploadFencesCompleted();
            releaseAndReturn(submission.Resources);
            SDL.ReleaseGPUFence(DeviceHandle, submission.Fence);
            submittedWork.RemoveAt(i);
        }
    }

    void WaitIdleAndReleaseSubmittedWork()
    {
        if (!SDL.WaitForGPUIdle(DeviceHandle))
            throw new InvalidOperationException($"Unable to wait for SDL GPU idle: {SDL.GetError()}");

        for (var i = 0; i < submittedWork.Count; ++i)
        {
            var submission = submittedWork[i];
            submission.MarkUploadFencesCompleted();
            releaseAndReturn(submission.Resources);
            SDL.ReleaseGPUFence(DeviceHandle, submission.Fence);
        }
        submittedWork.Clear();
        releaseBatchPool.Clear();
    }

    nint acquireCommandBuffer(string description)
    {
        var acquiredCommandBuffer = SDL.AcquireGPUCommandBuffer(DeviceHandle);
        if (acquiredCommandBuffer == nint.Zero)
            throw new InvalidOperationException($"Unable to acquire SDL GPU command buffer for {description}: {SDL.GetError()}");

        return acquiredCommandBuffer;
    }

    ResourceReleaseBatch rentReleaseBatch()
        => releaseBatchPool.Count != 0 ? releaseBatchPool.Pop() : new();

    void releaseAndReturn(ResourceReleaseBatch resources)
    {
        if (resources is null) return;

        resources.ReleaseAll(DeviceHandle);
        releaseBatchPool.Push(resources);
    }

    static void releaseResource(nint device, ResourceKind kind, nint handle)
    {
        switch (kind)
        {
            case ResourceKind.Buffer:
                SDL.ReleaseGPUBuffer(device, handle);
                break;

            case ResourceKind.TransferBuffer:
                SDL.ReleaseGPUTransferBuffer(device, handle);
                break;

            case ResourceKind.Texture:
                SDL.ReleaseGPUTexture(device, handle);
                break;

            case ResourceKind.Sampler:
                SDL.ReleaseGPUSampler(device, handle);
                break;

            case ResourceKind.Shader:
                SDL.ReleaseGPUShader(device, handle);
                break;

            case ResourceKind.GraphicsPipeline:
                SDL.ReleaseGPUGraphicsPipeline(device, handle);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    readonly record struct CopyPass(nint CommandBuffer, nint Handle, bool Standalone);

    struct SubmittedWork(nint fence, ResourceReleaseBatch resources, List<SdlUploadFence> uploadFences)
    {
        public readonly nint Fence = fence;
        public ResourceReleaseBatch Resources = resources;
        readonly List<SdlUploadFence> uploadFences = uploadFences;

        public void MarkUploadFencesCompleted()
        {
            if (uploadFences is null) return;

            for (var i = 0; i < uploadFences.Count; ++i)
                uploadFences[i].MarkCompleted();
            uploadFences.Clear();
        }
    }

    enum ResourceKind
    {
        Buffer,
        TransferBuffer,
        Texture,
        Sampler,
        Shader,
        GraphicsPipeline
    }

    sealed class ResourceReleaseBatch
    {
        readonly List<ResourceRelease> releases = [];

        public void Add(ResourceKind kind, nint handle)
        {
            if (handle == nint.Zero) return;

            releases.Add(new(kind, handle));
        }

        public void ReleaseAll(nint device)
        {
            releaseAll(device, ResourceKind.GraphicsPipeline);
            releaseAll(device, ResourceKind.Shader);
            releaseAll(device, ResourceKind.Sampler);
            releaseAll(device, ResourceKind.Texture);
            releaseAll(device, ResourceKind.Buffer);
            releaseAll(device, ResourceKind.TransferBuffer);
            releases.Clear();
        }

        void releaseAll(nint device, ResourceKind kind)
        {
            for (var i = 0; i < releases.Count; ++i)
            {
                var release = releases[i];
                if (release.Kind == kind)
                    releaseResource(device, kind, release.Handle);
            }
        }

        readonly record struct ResourceRelease(ResourceKind Kind, nint Handle);
    }
}
