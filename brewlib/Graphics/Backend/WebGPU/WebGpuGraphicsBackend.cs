namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using IO;
using Renderers;
using SDL3;
using Shaders;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Textures;
using WgpuSurface = Silk.NET.WebGPU.Surface;

public unsafe sealed partial class WebGpuGraphicsBackend : IGraphicsBackend, IRendererFactory
{
    const int MaxFragmentTextureBindings = 16;
    const int MaxNativeFragmentTextureBindings = 512;
    const bool PreferNativeTextureBindingArrays = true;
    static bool devicePollUnavailable;
    readonly WebGpuDeferredReleases deferredReleases = new();
    readonly SemaphoreSlim frameSubmissionRequested = new(0);
    readonly Lock frameSubmissionSync = new();

    readonly WebGpuGraphicsDevice graphicsDevice;
    readonly ConcurrentQueue<PendingFrameSubmission> pendingFrameSubmissions = [];
    readonly List<WebGpuRenderPipeline> pipelinesNeedingUniformFlush = [];
    readonly bool preferLowLatency;
    readonly WebGpuRenderPassState renderPassState = new();
    readonly List<WebGpuResourceSet> resourceSetsWithTextureCaches = [];
    readonly List<WebGpuTransientGraphicsBuffer> transientBuffersNeedingFlush = [];

    readonly nint windowHandle;
    Adapter* adapter;
    CompositeAlphaMode configuredAlphaMode;
    PresentMode configuredPresentMode;
    TextureFormat configuredSurfaceFormat;
    uint configuredSurfaceWidth, configuredSurfaceHeight;
    PfnDeviceLostCallback deviceLostCallback;

    bool disposed, frameActive, framebufferHasContents, surfaceConfigured;
    PfnErrorCallback errorCallback, errorScopeCallback;
    Vector4 frameClearColor;
    Exception frameSubmissionException;
    Thread frameSubmissionThread;
    Texture* frameTexture;
    TextureView* frameTextureView;
    Instance* instance;
    uint lastSubmissionPollFrameSerial;
    int maxFragmentTextureBindings = MaxFragmentTextureBindings;
    nint metalView;
    SurfaceGetCurrentTextureStatus preparedSurfaceStatus;
    Texture* preparedSurfaceTexture;
    TextureView* preparedSurfaceTextureView;

    PfnRequestAdapterCallback requestAdapterCallback;
    PfnRequestDeviceCallback requestDeviceCallback;
    bool supportsNativeNonUniformTextureIndexing;
    WgpuSurface* surface;
    Exception surfaceAcquireException;
    bool surfaceAcquireInFlight, surfaceAcquireHasResult, surfaceAcquireStop, surfaceSubmissionInFlight, frameSubmissionStop;
    Thread surfaceAcquireThread;
    SurfaceCapabilities surfaceCapabilities;
    SurfaceConfiguration surfaceConfiguration;

    public WebGpuGraphicsBackend(nint window = 0, bool preferLowLatency = false)
    {
        windowHandle = window;
        this.preferLowLatency = preferLowLatency;

        graphicsDevice = new(this);
        Device = graphicsDevice;
        Buffers = new WebGpuGraphicsBufferFactory(this);
        TransientBuffers = new WebGpuTransientGraphicsBufferFactory(this);
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
            64 * 1024);
    }

    internal WebGPU Api { get; private set; }

    internal Device* DeviceHandle { get; private set; }

    internal Queue* QueueHandle { get; private set; }

    internal TextureFormat SurfaceFormat { get; private set; }

    internal CommandEncoder* CommandEncoder { get; private set; }

    internal RenderPassEncoder* RenderPass { get; private set; }

    internal int MaxBufferSize { get; private set; } = 256 * 1024 * 1024;

    internal uint FrameSerial { get; private set; }
    internal uint RenderPassSerial { get; private set; }
    internal uint FramebufferWidth { get; private set; }

    internal uint FramebufferHeight { get; private set; }

    internal bool HasActiveFrame => CommandEncoder is not null;
    internal bool UseNativeNonUniformTextureIndexing { get; private set; }

    internal bool UsePartiallyBoundNativeTextureArrays { get; private set; }

    public string Name => "WebGPU";
    public GraphicsBackendCapabilities Capabilities { get; private set; }
    public ShaderSourceLanguage ShaderSourceLanguage => ShaderSourceLanguage.Wgsl;
    public IGraphicsDevice Device { get; }
    public IRendererFactory RendererFactory => this;
    public IGraphicsBufferFactory Buffers { get; }
    public ITransientGraphicsBufferFactory TransientBuffers { get; }
    public IRenderPipelineFactory RenderPipelines { get; }
    public IShaderAssetLoader ShaderAssets { get; } = new EmbeddedShaderAssetLoader();
    public IShaderProgramFactory ShaderPrograms => throw prototype();
    public ITextureFactory TextureFactory { get; }
    public IAsyncTextureUploader TextureUploader { get; }
    public bool PrefersBufferedTransientDraws => true;
    public bool PrefersDeferredRendererFlushes => true;

    public bool SupportsShaderExtension(string extensionName)
        => false;

    public void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Api is not null) return;

        Api = WebGPU.GetApi();
        createInstanceAndAdapter();
        DeviceHandle = requestDevice();
        loadDeviceLimits();
        QueueHandle = Api.DeviceGetQueue(DeviceHandle);

        errorCallback = new(onUncapturedError);
        Api.DeviceSetUncapturedErrorCallback(DeviceHandle, errorCallback, null);

        if (surface is not null)
            initializeSurface();

        rebuildCapabilities();
        logBackendCapabilities();

        DrawState.Initialize(resourceContainer, textureContainer, this);
    }

    public bool BeginFrame(Vector4 clearColor)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (frameActive)
            throw new InvalidOperationException("A WebGPU frame is already active");

        if (surface is null)
            throw new NotSupportedException("The WebGPU backend needs an SDL window before it can render frames");

        throwPendingFrameSubmissionException();
        releaseCompletedDeferredResources();
        submitPendingDeferredReleases();

        if (FramebufferWidth == 0 || FramebufferHeight == 0)
            return false;

        requestSurfaceTextureAcquire();

        CommandEncoder = Api.DeviceCreateCommandEncoder(DeviceHandle, null);
        if (CommandEncoder is null)
            throw new InvalidOperationException("Unable to create WebGPU command encoder");

        ++FrameSerial;
        frameActive = true;
        framebufferHasContents = false;
        frameClearColor = clearColor;
        return true;
    }

    public void EndFrame(bool discardFramebuffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!frameActive) return;

        if (!framebufferHasContents && RenderPass is null)
            TryRequireRenderPass(out _, false);

        EndRenderPass();
        flushPendingPipelineUniforms();
        flushPendingTransientUploads();

        submitFrameCommandEncoder(framebufferHasContents);

        frameActive = false;
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        EndRenderPass();

        if (CommandEncoder is not null)
        {
            Api.CommandEncoderRelease(CommandEncoder);
            CommandEncoder = null;
        }

        stopSurfaceTextureAcquire();
        stopFrameSubmission();
        surfaceAcquireRequested.Dispose();
        frameSubmissionRequested.Dispose();
        surfaceAcquireReady.Dispose();
        releasePreparedSurfaceTexture();
        releaseFrameSurfaceTexture();

        submitPendingDeferredReleases();
        waitForDeferredReleases();

        if (surface is not null)
        {
            Api.SurfaceUnconfigure(surface);
            surfaceConfigured = false;

            Api.SurfaceRelease(surface);
            surface = null;
        }

        if (metalView != 0)
        {
            SDL.MetalDestroyView(metalView);
            metalView = 0;
        }

        if (QueueHandle is not null)
        {
            Api.QueueRelease(QueueHandle);
            QueueHandle = null;
        }

        if (DeviceHandle is not null)
        {
            Api.DeviceRelease(DeviceHandle);
            DeviceHandle = null;
        }

        if (adapter is not null)
        {
            Api.AdapterRelease(adapter);
            adapter = null;
        }

        if (instance is not null)
        {
            Api.InstanceRelease(instance);
            instance = null;
        }

        errorCallback = default;
        errorScopeCallback = default;
        deviceLostCallback = default;
        requestAdapterCallback = default;
        requestDeviceCallback = default;
    }

    public IQuadRenderer CreateQuadRenderer()
        => new TexturedQuadRenderer(this);

    public ILineRenderer CreateLineRenderer()
        => new LineRenderer(this);

    public void Resize(uint width, uint height)
    {
        if (FramebufferWidth == width && FramebufferHeight == height) return;

        waitForSurfaceTextureAcquire();
        releasePreparedSurfaceTexture();
        releaseFrameSurfaceTexture();

        FramebufferWidth = width;
        FramebufferHeight = height;
        if (surface is not null && DeviceHandle is not null)
        {
            configureSurface();
            requestSurfaceTextureAcquire();
        }
    }

    internal string CaptureValidationError(Action action)
    {
        if (DeviceHandle is null) return null;

        string capturedMessage = null;
        var capturedType = ErrorType.NoError;

        using ManualResetEventSlim ready = new();
        errorScopeCallback = new((type, message, _) =>
        {
            capturedType = type;
            if (type != ErrorType.NoError)
                capturedMessage = SilkMarshal.PtrToString((nint)message) ?? string.Empty;

            ready.Set();
        });

        Api.DevicePushErrorScope(DeviceHandle, ErrorFilter.Validation);
        try
        {
            action();
        }
        finally
        {
            Api.DevicePopErrorScope(DeviceHandle, errorScopeCallback, null);
            ready.Wait();
            errorScopeCallback = default;
        }

        return capturedType is ErrorType.NoError
            ? null
            : $"{capturedType}: {capturedMessage}";
    }

    internal void RegisterTextureResourceSet(WebGpuResourceSet resourceSet)
        => resourceSetsWithTextureCaches.Add(resourceSet);

    internal void UnregisterTextureResourceSet(WebGpuResourceSet resourceSet)
        => resourceSetsWithTextureCaches.Remove(resourceSet);

    internal void PurgeCachedBindGroupsReferencing(TextureView* textureView, Sampler* sampler)
    {
        if (disposed || textureView is null && sampler is null) return;

        for (var i = resourceSetsWithTextureCaches.Count - 1; i >= 0; --i)
            resourceSetsWithTextureCaches[i].PurgeCachedBindGroupsReferencing(textureView, sampler);
    }

    internal void RegisterPipelineForUniformFlush(WebGpuRenderPipeline pipeline)
        => pipelinesNeedingUniformFlush.Add(pipeline);

    internal void RegisterTransientUpload(WebGpuTransientGraphicsBuffer buffer)
        => transientBuffersNeedingFlush.Add(buffer);

    void flushPendingPipelineUniforms()
    {
        foreach (var pipeline in pipelinesNeedingUniformFlush)
            pipeline.FlushUniforms();

        pipelinesNeedingUniformFlush.Clear();
    }

    void flushPendingTransientUploads()
    {
        foreach (var buffer in transientBuffersNeedingFlush)
            buffer.FlushPendingUploads();

        transientBuffersNeedingFlush.Clear();
    }

    static void onUncapturedError(ErrorType type, byte* message, void* userData)
    {
        var text = SilkMarshal.PtrToString((nint)message) ?? string.Empty;
        SDL.LogError(LogCategory.Render, $"WebGPU error: {type} {text}");
    }

    static NotImplementedException prototype()
        => new("The WebGPU backend is an opt-in prototype; only instance/device/surface setup is sketched in this pass.");
}