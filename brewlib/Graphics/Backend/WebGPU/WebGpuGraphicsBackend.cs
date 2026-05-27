namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Numerics;
using System.Threading;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using IO;
using Renderers;
using Shaders;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Textures;
using Buffer = Ahjo.Wgpu.Buffer;

public sealed partial class WebGpuGraphicsBackend : IGraphicsBackend, IRendererFactory
{
    const int MaxFragmentTextureBindings = 16;
    readonly WebGpuFrameEncoder frameEncoder;
    readonly WebGpuFrameResources frameResources = new();
    readonly WebGpuFrameSubmissionQueue frameSubmissions;

    readonly Lock queueSync = new();

    readonly nint windowHandle;
    Adapter adapter;
    int deviceLost;
    Instance instance;
    int maxFragmentTextureBindings = MaxFragmentTextureBindings;
    WebGpuTextureStagingBeltManager stagingBelt;
    WebGpuSurfaceManager surfaceManager;

    public WebGpuGraphicsBackend(nint window = 0,
        bool preferLowLatency = false,
        WebGpuFrameExecutionMode frameExecutionMode = WebGpuFrameExecutionMode.Threaded)
    {
        windowHandle = window;
        frameSubmissions = new(this, frameExecutionMode);
        DeferredReleases = new(this, queueSync, () => frameSubmissions.HasPending);

        GraphicsDevice = new(this);
        frameEncoder = new(this, GraphicsDevice);
        Device = GraphicsDevice;
        Buffers = new WebGpuGraphicsBufferFactory(this);
        RenderPipelines = new WebGpuRenderPipelineFactory(this);
        TextureFactory = new WebGpuTextureFactory(this);
        TextureUploader = new WebGpuAsyncTextureUploader(this);
        Capabilities = new(GraphicsBackendFeatures.TextureAtlases |
            GraphicsBackendFeatures.Instancing,
            16384,
            MaxFragmentTextureBindings,
            0,
            0,
            MaxFragmentTextureBindings,
            64 * 1024,
            4,
            0,
            8);
    }

    internal Device DeviceHandle { get; private set; }
    internal Queue QueueHandle => DeviceHandle?.Queue;
    internal WGPUTextureFormat SurfaceFormat => surfaceManager?.Format ?? default;
    internal int MaxBufferSize { get; private set; } = 256 * 1024 * 1024;
    internal int MaxBindGroups { get; private set; } = 4;

    internal int MaxVertexBuffers { get; private set; } = 8;

    internal int MaxBindingsPerBindGroup { get; private set; }

    internal uint FrameSerial { get; private set; }
    internal uint RenderPassSerial => frameEncoder.RenderPassSerial;
    internal uint FramebufferWidth => surfaceManager?.Width ?? 0;
    internal uint FramebufferHeight => surfaceManager?.Height ?? 0;
    internal bool IsDeviceLost => Volatile.Read(ref deviceLost) != 0;
    internal bool HasActiveFrame => frameEncoder.HasFrame;
    internal bool HasReadyRenderPass => frameEncoder.HasRenderPass;
    internal WebGpuGraphicsDevice GraphicsDevice { get; }

    internal TextureView FrameTextureView => surfaceManager?.FrameTextureView ?? default;
    internal bool IsDisposed { get; private set; }

    internal WebGpuVertexStagingBelt VertexStagingBelt { get; private set; }

    internal WebGpuDeferredReleaseManager DeferredReleases { get; }

    public string Name => "WebGPU";
    public GraphicsBackendCapabilities Capabilities { get; private set; }
    public ShaderSourceLanguage ShaderSourceLanguage => ShaderSourceLanguage.Wgsl;
    public IGraphicsDevice Device { get; }
    public IRendererFactory RendererFactory => this;
    public IGraphicsBufferFactory Buffers { get; }
    public IRenderPipelineFactory RenderPipelines { get; }
    public IShaderAssetLoader ShaderAssets { get; } = new EmbeddedShaderAssetLoader();
    public IShaderProgramFactory ShaderPrograms => throw prototype();
    public ITextureFactory TextureFactory { get; }
    public IAsyncTextureUploader TextureUploader { get; }

    public bool SupportsShaderExtension(string extensionName) => false;

    public void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (instance is not null) return;

        instance = WebGpuInitializer.CreateInstance();
        if (windowHandle != 0)
            surfaceManager = new(this, instance, windowHandle);

        adapter = instance.RequestAdapterBlocking(surfaceManager?.Surface ?? default);
        DeviceHandle = adapter.RequestDeviceBlocking();

        stagingBelt = new(this);
        VertexStagingBelt = new(this);
        loadDeviceLimits();

        surfaceManager?.Initialize(adapter);

        rebuildCapabilities();
        logBackendCapabilities();

        DrawState.Initialize(resourceContainer, textureContainer, this);
    }

    public bool BeginFrame(Vector4 clearColor)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (surfaceManager?.HasSurface != true)
            throw new NotSupportedException("The WebGPU backend needs an SDL window before it can render frames");

        frameSubmissions.ThrowPendingException();
        ThrowIfDeviceLost();
        DeferredReleases.Schedule(true);

        if (FramebufferWidth == 0 || FramebufferHeight == 0)
            return false;

        surfaceManager.RequestAcquire();
        frameEncoder.Begin(surfaceManager.IsFormatSrgb ? SrgbColorSpace.ToLinear(clearColor) : clearColor);
        ++FrameSerial;
        return true;
    }

    public void EndFrame(bool discardFramebuffer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (!frameEncoder.HasFrame) return;

        if (IsDeviceLost)
        {
            frameEncoder.DropAfterDeviceLoss();
            return;
        }

        var submissionSlotClaimed = false;
        try
        {
            if (!frameEncoder.HasContents && !frameEncoder.HasRenderPass)
                TryRequireRenderPass();

            frameEncoder.EndRenderPass();

            submissionSlotClaimed = frameSubmissions.TryClaimSubmissionSlot();
            if (!submissionSlotClaimed)
            {
                frameEncoder.DropAfterDeviceLoss();
                return;
            }

            frameSubmissions.EnqueueClaimed(new(
                frameEncoder.End(!discardFramebuffer, frameResources.DetachFlushes())));

            submissionSlotClaimed = false;
        }
        catch (Exception ex)
        {
            if (submissionSlotClaimed)
                frameSubmissions.CancelClaimedSubmissionSlot();

            MarkDeviceLost(ex);
            throw;
        }
        finally
        {
            frameEncoder.DisposeActiveHandles();
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        if (frameEncoder.HasFrame)
            EndFrame(true);

        IsDisposed = true;

        frameSubmissions.Dispose();
        surfaceManager?.Dispose();
        surfaceManager = null;

        if (stagingBelt is not null)
        {
            DeferredReleases.Retire(stagingBelt);
            stagingBelt = null;
        }

        if (VertexStagingBelt is not null)
        {
            DeferredReleases.Retire(VertexStagingBelt);
            VertexStagingBelt = null;
        }

        frameResources.Dispose();

        DeferredReleases.WaitForAll();
        DeferredReleases.Dispose();

        DeviceHandle?.Dispose();
        DeviceHandle = null;
        adapter?.Dispose();
        adapter = null;
        instance?.Dispose();
        instance = null;
    }

    public IQuadRenderer CreateQuadRenderer()
        => new TexturedQuadRenderer(this);

    public ILineRenderer CreateLineRenderer()
        => new LineRenderer(this);

    public void Resize(uint width, uint height)
        => surfaceManager?.Resize(width, height);

    internal void MarkDeviceLost(Exception exception = null)
    {
        if (Interlocked.Exchange(ref deviceLost, 1) != 0) return;

        frameEncoder.DropAfterDeviceLoss();
        DeferredReleases.ClearWithoutRelease();

        surfaceManager?.DropAfterDeviceLoss();

        if (exception is not null)
            frameSubmissions.SetPendingException(exception);
    }

    internal void ThrowIfDeviceLost()
    {
        if (IsDeviceLost)
            throw new InvalidOperationException("WebGPU device has been lost. Recreate the graphics backend/device before continuing.");
    }

    internal bool TryRequireRenderPass()
        => frameEncoder.TryRequireRenderPass();

    internal void SetRenderPipeline(RenderPipeline pipeline)
        => frameEncoder.SetRenderPipeline(pipeline);

    internal void SetBindGroup(uint slot, BindGroup bindGroup)
        => frameEncoder.SetBindGroup(slot, bindGroup);

    internal void SetBindGroup(uint slot, BindGroup bindGroup, uint dynamicOffset)
        => frameEncoder.SetBindGroup(slot, bindGroup, dynamicOffset);

    internal void SetVertexBuffer(uint slot, Buffer buffer, ulong offset, ulong size)
        => frameEncoder.SetVertexBuffer(slot, buffer, offset, size);

    internal void SetViewport(float x, float y, float width, float height, float minDepth = 0, float maxDepth = 1)
        => frameEncoder.SetViewport(x, y, width, height, minDepth, maxDepth);

    internal void SetScissorRect(uint x, uint y, uint width, uint height)
        => frameEncoder.SetScissorRect(x, y, width, height);

    internal void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        => frameEncoder.Draw(vertexCount, instanceCount, firstVertex, firstInstance);

    internal void SetIndexBuffer(Buffer buffer, WGPUIndexFormat format, ulong offset, ulong size)
        => frameEncoder.SetIndexBuffer(buffer, format, offset, size);

    internal void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int baseVertex = 0, uint firstInstance = 0)
        => frameEncoder.DrawIndexed(indexCount, instanceCount, firstIndex, baseVertex, firstInstance);

    internal void UploadTextureWithStagingBelt(Texture texture,
        WGPUTextureFormat format,
        ReadOnlySpan<byte> packedData,
        int width,
        int height,
        int x,
        int y)
        => stagingBelt.WriteTexture(texture, format, packedData, width, height, x, y);

    internal void UploadTextureRowsWithStagingBelt(Texture texture,
        WGPUTextureFormat format,
        Image<Rgba32> bitmap,
        int width,
        int height,
        int x,
        int y)
        => stagingBelt.WriteTextureRows(texture, format, bitmap, width, height, x, y);

    internal void PollStagingBelt()
        => stagingBelt?.Poll();

    internal void RegisterCachedResourceSet(IWebGpuCachedResourceSet resourceSet)
        => frameResources.RegisterCachedResourceSet(resourceSet);

    internal void UnregisterCachedResourceSet(IWebGpuCachedResourceSet resourceSet)
        => frameResources.UnregisterCachedResourceSet(resourceSet);

    internal void PurgeCachedBindGroupsReferencing(WebGpuResourceReference resource)
    {
        if (!IsDisposed)
            frameResources.PurgeCachedBindGroupsReferencing(resource);
    }

    internal void RegisterPipelineForUniformFlush(WebGpuRenderPipeline pipeline)
        => frameResources.RegisterPipelineForUniformFlush(pipeline);

    internal void RegisterBufferUpload(WebGpuGraphicsBuffer buffer)
        => frameResources.RegisterBufferUpload(buffer);

    internal void RegisterVertexUpload(WebGpuVertexUpload upload)
        => frameResources.RegisterVertexUpload(upload);

    internal bool TryPrepareFrameSurfaceTexture(bool waitForAcquire = false)
        => surfaceManager.TryPrepareFrameTexture(waitForAcquire);

    internal void PresentSubmittedSurfaceTexture(Texture texture, TextureView textureView)
        => surfaceManager.PresentSubmittedTexture(texture, textureView);

    internal void ReleaseSurfaceTexture(Texture texture, TextureView textureView)
        => surfaceManager.ReleaseTexture(texture, textureView);

    internal void CompleteSurfaceSubmission(Texture texture, TextureView textureView)
        => surfaceManager.CompleteSubmission(texture, textureView);

    internal CommandBuffer EncodeRecordedFrame(WebGpuRecordedFrame recordedFrame, out WebGpuSurfaceFrame surfaceFrame)
    {
        VertexStagingBelt.Poll();

        surfaceFrame = default;
        if (IsDeviceLost || !recordedFrame.NeedsRenderPass)
        {
            recordedFrame.Release();
            return default;
        }

        if (recordedFrame.PresentSurfaceTexture)
        {
            if (!surfaceManager.TryPrepareFrameTexture(true))
                return default;

            surfaceFrame = surfaceManager.DetachFrameTexture(true);
        }

        var targetView = recordedFrame.PresentSurfaceTexture
            ? surfaceFrame.TextureView
            : FrameTextureView;

        if (targetView.IsNull)
        {
            recordedFrame.Release();
            return default;
        }

        var encoder = DeviceHandle.CreateCommandEncoder();
        var encoderCreated = true;
        try
        {
            recordedFrame.Flushes?.Flush(this, encoder);
            VertexStagingBelt.Finish();

            var pass = encoder.BeginRenderPass([
                new(targetView,
                    WGPULoadOp.Clear,
                    WGPUStoreOp.Store,
                    new()
                    {
                        r = recordedFrame.ClearColor.X,
                        g = recordedFrame.ClearColor.Y,
                        b = recordedFrame.ClearColor.Z,
                        a = recordedFrame.ClearColor.W
                    })
            ]);

            recordedFrame.Commands.Replay(pass);
            pass.Dispose();

            var commandBuffer = encoder.Finish();
            encoderCreated = false;
            return commandBuffer;
        }
        finally
        {
            recordedFrame.Release();
            if (encoderCreated && !IsDeviceLost)
                encoder.Dispose();
        }
    }

    internal void SubmitCommandBuffer(CommandBuffer commandBuffer)
    {
        if (IsDeviceLost || commandBuffer.IsNull) return;

        try
        {
            lock (queueSync)
            {
                QueueHandle.Submit(commandBuffer);
                DeferredReleases.NotifyQueueSubmitted();
            }
        }
        catch (Exception ex)
        {
            MarkDeviceLost(ex);
            throw;
        }
        finally
        {
            if (!IsDeviceLost)
                commandBuffer.Dispose();
        }
    }

    static NotImplementedException prototype()
        => new("The WebGPU backend is an opt-in prototype.");
}