namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using IO;
using Renderers;
using SDL3;
using Shaders;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Textures;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;
using WgpuSurface = Silk.NET.WebGPU.Surface;

public unsafe sealed partial class WebGpuGraphicsBackend : IGraphicsBackend, IRendererFactory
{
    const int MaxFragmentTextureBindings = 16;
    const int MaxNativeFragmentTextureBindings = 512;
    const uint ResourceRetirementFrameLag = 3;
    const bool PreferNativeTextureBindingArrays = true;

    readonly nint windowHandle;
    readonly bool preferLowLatency;

    WebGPU wgpu;
    Instance* instance;
    Adapter* adapter;
    Device* device;
    Queue* queue;
    WgpuSurface* surface;
    SurfaceCapabilities surfaceCapabilities;
    SurfaceConfiguration surfaceConfiguration;
    TextureFormat surfaceFormat;
    CommandEncoder* commandEncoder;
    RenderPassEncoder* renderPass;
    Texture* frameTexture;
    TextureView* frameTextureView;
    Texture* preparedSurfaceTexture;
    TextureView* preparedSurfaceTextureView;
    SurfaceGetCurrentTextureStatus preparedSurfaceStatus;
    Exception surfaceAcquireException;
    Exception frameSubmissionException;
    Thread surfaceAcquireThread;
    Thread frameSubmissionThread;
    nint metalView;
    readonly WebGpuRenderPassState renderPassState = new();
    readonly List<WebGpuRenderPipeline> pipelinesNeedingUniformFlush = [];
    readonly List<WebGpuTransientGraphicsBuffer> transientBuffersNeedingFlush = [];
    readonly List<WebGpuResourceSet> resourceSetsWithTextureCaches = [];
    readonly Lock frameSubmissionSync = new();
    readonly Queue<PendingFrameSubmission> pendingFrameSubmissions = [];
    readonly SemaphoreSlim frameSubmissionRequested = new(0);
    WebGpuRetiredResources pendingRetiredResources = new();
    readonly List<WebGpuRetiredResources> retiredResourceBatches = [];

    PfnRequestAdapterCallback requestAdapterCallback;
    PfnRequestDeviceCallback requestDeviceCallback;
    PfnDeviceLostCallback deviceLostCallback;
    PfnErrorCallback errorCallback, errorScopeCallback;

    readonly WebGpuGraphicsDevice graphicsDevice;
    static bool devicePollUnavailable;

    bool disposed, frameActive, framebufferHasContents, surfaceConfigured;
    bool hasPendingRetiredResources;
    bool supportsNativeNonUniformTextureIndexing;
    bool nativeTextureBindingArraysEnabled, nativeTextureBindingArraysPartiallyBound;
    bool surfaceAcquireInFlight, surfaceAcquireHasResult, surfaceAcquireStop, surfaceSubmissionInFlight, frameSubmissionStop;
    uint lastSubmissionPollFrameSerial;
    uint nextRetiredResourceReleaseFrameSerial = uint.MaxValue;
    Vector4 frameClearColor;
    uint frameWidth, frameHeight, configuredSurfaceWidth, configuredSurfaceHeight;
    TextureFormat configuredSurfaceFormat;
    PresentMode configuredPresentMode;
    CompositeAlphaMode configuredAlphaMode;
    int maxBufferSize = 256 * 1024 * 1024;
    int maxFragmentTextureBindings = MaxFragmentTextureBindings;

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

    internal WebGPU Api => wgpu;
    internal Device* DeviceHandle => device;
    internal Queue* QueueHandle => queue;
    internal TextureFormat SurfaceFormat => surfaceFormat;
    internal CommandEncoder* CommandEncoder => commandEncoder;
    internal RenderPassEncoder* RenderPass => renderPass;
    internal int MaxBufferSize => maxBufferSize;
    internal uint FrameSerial { get; private set; }
    internal uint RenderPassSerial { get; private set; }
    internal uint FramebufferWidth => frameWidth;
    internal uint FramebufferHeight => frameHeight;
    internal bool HasActiveFrame => commandEncoder is not null;
    internal bool UseNativeNonUniformTextureIndexing => nativeTextureBindingArraysEnabled;
    internal bool UsePartiallyBoundNativeTextureArrays => nativeTextureBindingArraysPartiallyBound;

    public bool SupportsShaderExtension(string extensionName)
        => false;

    public void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (wgpu is not null) return;
        
        wgpu = WebGPU.GetApi();
        createInstanceAndAdapter();
        device = requestDevice();
        loadDeviceLimits();
        queue = wgpu.DeviceGetQueue(device);

        errorCallback = new PfnErrorCallback(onUncapturedError);
        wgpu.DeviceSetUncapturedErrorCallback(device, errorCallback, null);

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
        submitPendingRetiredResources();
        releaseReadyRetiredResources();

        if (frameWidth == 0 || frameHeight == 0)
            return false;

        requestSurfaceTextureAcquire();

        commandEncoder = wgpu.DeviceCreateCommandEncoder(device, null);
        if (commandEncoder is null)
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

        if (!framebufferHasContents && renderPass is null)
            TryRequireRenderPass(out _, false);

        EndRenderPass();
        flushPendingPipelineUniforms();
        flushPendingTransientUploads();

        submitFrameCommandEncoder(framebufferHasContents);

        submitPendingRetiredResources();

        frameActive = false;
    }

    void submitFrameCommandEncoder(bool presentSurfaceTexture)
    {
        var submission = new PendingFrameSubmission(commandEncoder,
            presentSurfaceTexture ? frameTexture : null,
            presentSurfaceTexture ? frameTextureView : null,
            presentSurfaceTexture,
            FrameSerial);

        commandEncoder = null;
        frameTexture = null;
        frameTextureView = null;
        if (presentSurfaceTexture)
            lock (surfaceAcquireSync)
                surfaceSubmissionInFlight = true;

        startFrameSubmission();
        lock (frameSubmissionSync)
            pendingFrameSubmissions.Enqueue(submission);
        frameSubmissionRequested.Release();
    }

    void submitCommandEncoder(CommandEncoder* encoder)
    {
        CommandBuffer* commandBuffer = null;
        try
        {
            commandBuffer = finishCommandEncoder(encoder);
            submitCommandBuffer(commandBuffer);
        }
        finally
        {
            releaseSubmittedCommandResources(encoder, commandBuffer);
        }
    }

    CommandBuffer* finishCommandEncoder(CommandEncoder* encoder)
    {
        var commandBuffer = wgpu.CommandEncoderFinish(encoder, null);
        if (commandBuffer is null)
            throw new InvalidOperationException("Unable to finish WebGPU command encoder");

        return commandBuffer;
    }

    void submitCommandBuffer(CommandBuffer* commandBuffer)
        => wgpu.QueueSubmit(queue, 1, &commandBuffer);

    internal void SubmitImmediateCommandEncoder(CommandEncoder* encoder)
    {
        submitCommandEncoder(encoder);
        pollDevice();
    }

    void releaseSubmittedCommandResources(CommandEncoder* encoder, CommandBuffer* commandBuffer)
    {
        if (commandBuffer is not null)
            wgpu.CommandBufferRelease(commandBuffer);
        if (encoder is not null)
            wgpu.CommandEncoderRelease(encoder);
    }

    void startFrameSubmission()
    {
        if (frameSubmissionThread is not null)
            return;

        frameSubmissionThread = new(frameSubmissionLoop)
        {
            IsBackground = true,
            Name = "storybrew WebGPU frame submit"
        };
        frameSubmissionThread.Start();
    }

    void frameSubmissionLoop()
    {
        while (true)
        {
            frameSubmissionRequested.Wait();

            PendingFrameSubmission submission;
            lock (frameSubmissionSync)
            {
                if (frameSubmissionStop && pendingFrameSubmissions.Count == 0)
                    return;
                if (pendingFrameSubmissions.Count == 0)
                    continue;

                submission = pendingFrameSubmissions.Dequeue();
            }

            submitFrameSubmission(submission);
        }
    }

    void submitFrameSubmission(PendingFrameSubmission submission)
    {
        try
        {
            submitCommandEncoder(submission.CommandEncoder);
            if (submission.PresentSurfaceTexture)
                presentSubmittedSurfaceTexture(submission.SurfaceTexture, submission.SurfaceTextureView);
            else
                releaseSurfaceTexture(submission.SurfaceTexture, submission.SurfaceTextureView);

            pollDeviceAfterSubmission(submission.FrameSerial);
        }
        catch (Exception ex)
        {
            lock (frameSubmissionSync)
                frameSubmissionException ??= ex;
            if (submission.PresentSurfaceTexture)
                completeSurfaceSubmission(submission.SurfaceTexture, submission.SurfaceTextureView);
            else
                releaseSurfaceTexture(submission.SurfaceTexture, submission.SurfaceTextureView);
        }
    }

    void throwPendingFrameSubmissionException()
    {
        Exception exception;
        lock (frameSubmissionSync)
        {
            exception = frameSubmissionException;
            frameSubmissionException = null;
        }

        if (exception is not null)
            throw new InvalidOperationException("WebGPU frame submission failed", exception);
    }

    void stopFrameSubmission()
    {
        lock (frameSubmissionSync)
            frameSubmissionStop = true;

        frameSubmissionRequested.Release();
        frameSubmissionThread?.Join();
        frameSubmissionThread = null;
    }

    public IQuadRenderer CreateQuadRenderer()
        => new TexturedQuadRenderer(this);

    public ILineRenderer CreateLineRenderer()
        => new LineRenderer(this);

    public void Resize(uint width, uint height)
    {
        if (frameWidth == width && frameHeight == height) return;
        waitForSurfaceTextureAcquire();
        releasePreparedSurfaceTexture();
        releaseFrameSurfaceTexture();

        frameWidth = width;
        frameHeight = height;
        if (surface is not null && device is not null)
        {
            configureSurface();
            requestSurfaceTextureAcquire();
        }
    }

    [DllImport("wgpu_native", EntryPoint = "wgpuDevicePoll", CallingConvention = CallingConvention.Cdecl)]
    static extern byte wgpuDevicePoll(nint device, byte wait, nint wrappedSubmissionIndex);

    void waitForDeviceIdle()
    {
        if (device is null) return;
        try { wgpuDevicePoll((nint)device, 1, 0); }
        catch (DllNotFoundException) { /* binding unavailable */ }
        catch (EntryPointNotFoundException) { /* binding lacks DevicePoll */ }
    }

    void pollDevice()
    {
        if (device is null || devicePollUnavailable) return;

        try { wgpuDevicePoll((nint)device, 0, 0); }
        catch (DllNotFoundException) { devicePollUnavailable = true; }
        catch (EntryPointNotFoundException) { devicePollUnavailable = true; }
    }

    void pollDeviceAfterSubmission(uint submittedFrameSerial)
    {
        if (submittedFrameSerial == lastSubmissionPollFrameSerial)
            return;

        lastSubmissionPollFrameSerial = submittedFrameSerial;
        pollDevice();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        EndRenderPass();

        if (commandEncoder is not null)
        {
            // Drop the in-progress encoder without submitting its commands;
            // anything it referenced is already in our retirement queues.
            wgpu.CommandEncoderRelease(commandEncoder);
            commandEncoder = null;
        }

        stopSurfaceTextureAcquire();
        stopFrameSubmission();
        surfaceAcquireRequested.Dispose();
        frameSubmissionRequested.Dispose();
        surfaceAcquireReady.Dispose();
        releasePreparedSurfaceTexture();
        releaseFrameSurfaceTexture();

        waitForDeviceIdle();

        submitPendingRetiredResources();
        releaseAllRetiredResources();

        // Disarm the error callback before tearing down; otherwise device-internal
        // teardown errors fire onUncapturedError on a freed delegate.
        if (device is not null)
            wgpu.DeviceSetUncapturedErrorCallback(device, default, null);

        if (surface is not null)
        {
            wgpu.SurfaceUnconfigure(surface);
            surfaceConfigured = false;
            wgpu.SurfaceRelease(surface);
            surface = null;
        }

        if (metalView != 0)
        {
            SDL.MetalDestroyView(metalView);
            metalView = 0;
        }

        if (queue is not null)
        {
            wgpu.QueueRelease(queue);
            queue = null;
        }

        if (device is not null)
        {
            wgpu.DeviceRelease(device);
            device = null;
        }

        if (adapter is not null)
        {
            wgpu.AdapterRelease(adapter);
            adapter = null;
        }

        if (instance is not null)
        {
            wgpu.InstanceRelease(instance);
            instance = null;
        }

        wgpu?.Dispose();
        wgpu = null;

        errorCallback = default;
        errorScopeCallback = default;
        deviceLostCallback = default;
        requestAdapterCallback = default;
        requestDeviceCallback = default;
    }

    internal string CaptureValidationError(Action action)
    {
        if (device is null) return null;

        string capturedMessage = null;
        var capturedType = ErrorType.NoError;

        using ManualResetEventSlim ready = new();
        errorScopeCallback = new PfnErrorCallback((type, message, _) =>
        {
            capturedType = type;
            if (type != ErrorType.NoError)
                capturedMessage = SilkMarshal.PtrToString((nint)message) ?? string.Empty;

            ready.Set();
        });

        wgpu.DevicePushErrorScope(device, ErrorFilter.Validation);
        try
        {
            action();
        }
        finally
        {
            wgpu.DevicePopErrorScope(device, errorScopeCallback, null);
            var spinWait = new SpinWait();
            while (!ready.IsSet)
            {
                try { wgpuDevicePoll((nint)device, 0, 0); }
                catch (DllNotFoundException) { ready.Wait(); }
                catch (EntryPointNotFoundException) { ready.Wait(); }

                if (!ready.IsSet)
                    spinWait.SpinOnce();
            }
            errorScopeCallback = default;
        }

        return capturedType == ErrorType.NoError
            ? null
            : $"{capturedType}: {capturedMessage}";
    }

    internal bool IsFrameFenceSignaled(uint frameSerial)
        => unchecked(FrameSerial - frameSerial) >= 1;

    internal void RetireBindGroup(BindGroup* bindGroup)
    {
        if (bindGroup is not null)
        {
            pendingRetiredResources.BindGroups.Add(new(bindGroup));
            hasPendingRetiredResources = true;
        }
    }

    internal void RetireBuffer(WgpuBuffer* buffer)
    {
        if (buffer is not null)
        {
            pendingRetiredResources.Buffers.Add(new(buffer));
            hasPendingRetiredResources = true;
        }
    }

    internal void RetireRenderPipeline(RenderPipeline* renderPipeline)
    {
        if (renderPipeline is not null)
        {
            pendingRetiredResources.RenderPipelines.Add(new(renderPipeline));
            hasPendingRetiredResources = true;
        }
    }

    internal void RetireSampler(Sampler* sampler)
    {
        if (sampler is not null)
        {
            pendingRetiredResources.Samplers.Add(new(sampler));
            hasPendingRetiredResources = true;
        }
    }

    internal void RetireTexture(Texture* texture)
    {
        if (texture is not null)
        {
            pendingRetiredResources.Textures.Add(new(texture));
            hasPendingRetiredResources = true;
        }
    }

    internal void RetireTextureView(TextureView* textureView)
    {
        if (textureView is not null)
        {
            pendingRetiredResources.TextureViews.Add(new(textureView));
            hasPendingRetiredResources = true;
        }
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryGetReadyRenderPass(out RenderPassEncoder* readyRenderPass)
    {
        readyRenderPass = renderPass;
        return readyRenderPass is not null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryRequireRenderPass(out RenderPassEncoder* readyRenderPass, bool waitForSurfaceTexture = true)
    {
        readyRenderPass = renderPass;
        if (renderPass is not null)
            return true;

        if (commandEncoder is null)
            throw new InvalidOperationException("WebGPU render pass requested outside an active frame");

        if (frameTextureView is null && !tryPrepareFrameSurfaceTexture(waitForSurfaceTexture))
            return false;

        RenderPassColorAttachment colorAttachment = new()
        {
            View = frameTextureView,
            LoadOp = framebufferHasContents ? LoadOp.Load : LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new()
            {
                R = frameClearColor.X,
                G = frameClearColor.Y,
                B = frameClearColor.Z,
                A = frameClearColor.W
            }
        };

        RenderPassDescriptor descriptor = new()
        {
            ColorAttachmentCount = 1,
            ColorAttachments = &colorAttachment
        };

        renderPass = wgpu.CommandEncoderBeginRenderPass(commandEncoder, in descriptor);
        if (renderPass is null)
            throw new InvalidOperationException("Unable to begin WebGPU render pass");

        DrawState.CountDrawCall();

        ++RenderPassSerial;
        renderPassState.Reset();
        graphicsDevice.ApplyRenderPassState(renderPass);
        readyRenderPass = renderPass;
        return true;
    }

    internal void EndRenderPass()
    {
        if (renderPass is null) return;

        wgpu.RenderPassEncoderEnd(renderPass);
        wgpu.RenderPassEncoderRelease(renderPass);
        renderPass = null;
        framebufferHasContents = true;
    }

    internal void SetRenderPipeline(RenderPipeline* pipeline)
    {
        if (renderPassState.Pipeline == pipeline) return;

        wgpu.RenderPassEncoderSetPipeline(renderPass, pipeline);
        renderPassState.Pipeline = pipeline;
    }

    internal void SetBindGroup(uint slot, BindGroup* bindGroup)
    {
        var index = checked((int)slot);
        renderPassState.EnsureBindGroupSlot(index);
        ref var binding = ref renderPassState.BindGroups[index];
        if (binding.BindGroup == bindGroup &&
            binding.DynamicOffsetCount == 0)
            return;

        wgpu.RenderPassEncoderSetBindGroup(renderPass,
            slot,
            bindGroup,
            0,
            null);

        binding.BindGroup = bindGroup;
        binding.DynamicOffsetCount = 0;
        binding.DynamicOffset = 0;
    }

    internal void SetBindGroup(uint slot, BindGroup* bindGroup, uint dynamicOffset)
    {
        var index = checked((int)slot);
        renderPassState.EnsureBindGroupSlot(index);
        ref var binding = ref renderPassState.BindGroups[index];
        if (binding.BindGroup == bindGroup &&
            binding.DynamicOffsetCount == 1 &&
            binding.DynamicOffset == dynamicOffset)
            return;

        wgpu.RenderPassEncoderSetBindGroup(renderPass,
            slot,
            bindGroup,
            1,
            &dynamicOffset);

        binding.BindGroup = bindGroup;
        binding.DynamicOffsetCount = 1;
        binding.DynamicOffset = dynamicOffset;
    }

    internal void SetVertexBuffer(uint slot, WgpuBuffer* buffer, ulong offset, ulong size)
    {
        var index = checked((int)slot);
        renderPassState.EnsureVertexBufferSlot(index);
        ref var binding = ref renderPassState.VertexBuffers[index];
        if (binding.Buffer == buffer &&
            binding.Offset == offset &&
            binding.Size == size)
            return;

        wgpu.RenderPassEncoderSetVertexBuffer(renderPass, slot, buffer, offset, size);
        binding.Buffer = buffer;
        binding.Offset = offset;
        binding.Size = size;
    }

    void submitPendingRetiredResources()
    {
        if (!hasPendingRetiredResources) return;

        pendingRetiredResources.ReleaseFrameSerial = FrameSerial + ResourceRetirementFrameLag;
        retiredResourceBatches.Add(pendingRetiredResources);
        if (pendingRetiredResources.ReleaseFrameSerial < nextRetiredResourceReleaseFrameSerial)
            nextRetiredResourceReleaseFrameSerial = pendingRetiredResources.ReleaseFrameSerial;

        pendingRetiredResources = new();
        hasPendingRetiredResources = false;
    }

    void releaseReadyRetiredResources()
    {
        if (wgpu is null ||
            retiredResourceBatches.Count == 0 ||
            FrameSerial < nextRetiredResourceReleaseFrameSerial)
            return;

        var nextReleaseFrameSerial = uint.MaxValue;
        for (var i = retiredResourceBatches.Count - 1; i >= 0; --i)
        {
            var retiredResources = retiredResourceBatches[i];
            if (FrameSerial < retiredResources.ReleaseFrameSerial)
            {
                if (retiredResources.ReleaseFrameSerial < nextReleaseFrameSerial)
                    nextReleaseFrameSerial = retiredResources.ReleaseFrameSerial;
                continue;
            }

            retiredResources.Release(wgpu);
            retiredResourceBatches.RemoveAt(i);
        }

        nextRetiredResourceReleaseFrameSerial = nextReleaseFrameSerial;
    }

    void releaseAllRetiredResources()
    {
        if (wgpu is null)
        {
            retiredResourceBatches.Clear();
            pendingRetiredResources = new();
            hasPendingRetiredResources = false;
            nextRetiredResourceReleaseFrameSerial = uint.MaxValue;
            return;
        }

        pendingRetiredResources.Release(wgpu);
        pendingRetiredResources = new();
        hasPendingRetiredResources = false;

        foreach (var retiredResources in retiredResourceBatches)
            retiredResources.Release(wgpu);
        retiredResourceBatches.Clear();
        nextRetiredResourceReleaseFrameSerial = uint.MaxValue;
    }

    WgpuSurface* createSurface(nint window)
    {
        var properties = SDL.GetWindowProperties(window);
        if (properties == 0)
            throw new InvalidOperationException($"Unable to get SDL window properties: {SDL.GetError()}");

        var videoDriver = SDL.GetCurrentVideoDriver();
        var createdSurface = videoDriver switch
        {
            "windows" or "win32" => createWin32Surface(properties),
            "cocoa" or "uikit" => createMetalSurface(window),
            "wayland" => createWaylandSurface(properties),
            "x11" => createXlibSurface(properties),
            "android" => createAndroidSurface(properties),
            _ => tryCreateKnownSurface(properties, window)
        };

        return createdSurface is not null
            ? createdSurface
            : throw new PlatformNotSupportedException(
                $"No WebGPU surface path is available for SDL video driver '{videoDriver ?? "unknown"}'");
    }

    WgpuSurface* tryCreateKnownSurface(uint properties, nint window)
    {
        var createdSurface = createWin32Surface(properties);
        if (createdSurface is not null) return createdSurface;

        createdSurface = createWaylandSurface(properties);
        if (createdSurface is not null) return createdSurface;

        createdSurface = createXlibSurface(properties);
        if (createdSurface is not null) return createdSurface;

        return createAndroidSurface(properties);
    }

    WgpuSurface* createWin32Surface(uint properties)
    {
        var hwnd = SDL.GetPointerProperty(properties, SDL.Props.WindowWin32HWNDPointer, 0);
        var hinstance = SDL.GetPointerProperty(properties, SDL.Props.WindowWin32InstancePointer, 0);
        if (hwnd == 0 || hinstance == 0)
            return null;

        SurfaceDescriptorFromWindowsHWND hwndDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromWindowsHwnd
            },
            Hinstance = (void*)hinstance,
            Hwnd = (void*)hwnd
        };

        return createChainedSurface(&hwndDescriptor.Chain);
    }

    WgpuSurface* createMetalSurface(nint window)
    {
        metalView = SDL.MetalCreateView(window);
        if (metalView == 0)
            return null;

        var layer = SDL.MetalGetLayer(metalView);
        if (layer == 0)
        {
            SDL.MetalDestroyView(metalView);
            metalView = 0;
            return null;
        }

        SurfaceDescriptorFromMetalLayer metalDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromMetalLayer
            },
            Layer = (void*)layer
        };

        try
        {
            return createChainedSurface(&metalDescriptor.Chain);
        }
        catch
        {
            SDL.MetalDestroyView(metalView);
            metalView = 0;
            throw;
        }
    }

    WgpuSurface* createWaylandSurface(uint properties)
    {
        var display = SDL.GetPointerProperty(properties, SDL.Props.WindowWaylandDisplayPointer, 0);
        var surface = SDL.GetPointerProperty(properties, SDL.Props.WindowWaylandSurfacePointer, 0);
        if (display == 0 || surface == 0)
            return null;

        SurfaceDescriptorFromWaylandSurface waylandDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromWaylandSurface
            },
            Display = (void*)display,
            Surface = (void*)surface
        };

        return createChainedSurface(&waylandDescriptor.Chain);
    }

    WgpuSurface* createXlibSurface(uint properties)
    {
        var display = SDL.GetPointerProperty(properties, SDL.Props.WindowX11DisplayPointer, 0);
        var window = SDL.GetNumberProperty(properties, SDL.Props.WindowX11WindowNumber, 0);
        if (display == 0 || window == 0)
            return null;

        SurfaceDescriptorFromXlibWindow xlibDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromXlibWindow
            },
            Display = (void*)display,
            Window = (ulong)window
        };

        return createChainedSurface(&xlibDescriptor.Chain);
    }

    WgpuSurface* createAndroidSurface(uint properties)
    {
        var window = SDL.GetPointerProperty(properties, SDL.Props.WindowAndroidWindowPointer, 0);
        if (window == 0)
            return null;

        SurfaceDescriptorFromAndroidNativeWindow androidDescriptor = new()
        {
            Chain = new()
            {
                SType = SType.SurfaceDescriptorFromAndroidNativeWindow
            },
            Window = (void*)window
        };

        return createChainedSurface(&androidDescriptor.Chain);
    }

    WgpuSurface* createChainedSurface(ChainedStruct* nextInChain)
    {
        SurfaceDescriptor descriptor = new()
        {
            NextInChain = nextInChain
        };

        var createdSurface = wgpu.InstanceCreateSurface(instance, in descriptor);
        return createdSurface is not null
            ? createdSurface
            : throw new InvalidOperationException("Unable to create WebGPU surface from SDL window");
    }

    void createInstanceAndAdapter()
    {
        instance = createInstance();
        if (instance is null)
            throw new InvalidOperationException("Unable to create WebGPU instance");

        if (windowHandle != 0)
            surface = createSurface(windowHandle);

        adapter = requestAdapter();
        if (adapter is null)
            throw new InvalidOperationException("Unable to acquire WebGPU adapter");
    }

    Instance* createInstance()
    {
        InstanceDescriptor instanceDescriptor = new();
        return wgpu.CreateInstance(&instanceDescriptor);
    }

    Adapter* requestAdapter()
    {
        using ManualResetEventSlim ready = new();
        Adapter* requestedAdapter = null;

        requestAdapterCallback = new PfnRequestAdapterCallback((status, receivedAdapter, _, _) =>
        {
            if (status == RequestAdapterStatus.Success)
                requestedAdapter = receivedAdapter;
            ready.Set();
        });

        RequestAdapterOptions options = new()
        {
            CompatibleSurface = surface
        };

        wgpu.InstanceRequestAdapter(instance, in options, requestAdapterCallback, null);
        ready.Wait();
        return requestedAdapter;
    }

    Device* requestDevice()
    {
        var requestNativeNonUniformTextureIndexing =
            PreferNativeTextureBindingArrays && supportsAdapterNativeNonUniformTextureIndexing();
        var requestedNativeTextureBindings = 0;
        var requestedPartiallyBoundTextureBindings = false;
        Device* requestedDevice = null;

        if (requestNativeNonUniformTextureIndexing)
        {
            var adapterSupportsPartiallyBoundTextureBindings = supportsAdapterPartiallyBoundTextureBindingArrays();
            requestedDevice = tryRequestDeviceWithNativeTextureArrays(adapterSupportsPartiallyBoundTextureBindings,
                out requestedNativeTextureBindings);
            requestedPartiallyBoundTextureBindings =
                requestedDevice is not null && adapterSupportsPartiallyBoundTextureBindings;

            if (requestedDevice is null && adapterSupportsPartiallyBoundTextureBindings)
            {
                requestedDevice = tryRequestDeviceWithNativeTextureArrays(false,
                    out requestedNativeTextureBindings);
                requestedPartiallyBoundTextureBindings = false;
            }

            if (requestedDevice is null)
                SDL.LogWarn(LogCategory.Render,
                    "WebGPU adapter reports native texture binding arrays, but device creation failed with them enabled; retrying without them");
        }

        if (requestedDevice is null)
            requestedDevice = requestDevice(0);

        if (requestedDevice is null)
            throw new InvalidOperationException("Unable to acquire WebGPU device");

        supportsNativeNonUniformTextureIndexing = deviceSupportsNativeNonUniformTextureIndexing(requestedDevice);
        nativeTextureBindingArraysEnabled = requestedNativeTextureBindings > 0 && supportsNativeNonUniformTextureIndexing;
        nativeTextureBindingArraysPartiallyBound =
            nativeTextureBindingArraysEnabled &&
            requestedPartiallyBoundTextureBindings &&
            wgpu.DeviceHasFeature(requestedDevice,
                WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeaturePartiallyBoundBindingArray));
        return requestedDevice;
    }

    Device* tryRequestDeviceWithNativeTextureArrays(bool requestPartiallyBoundTextureBindings,
        out int requestedNativeTextureBindings)
    {
        requestedNativeTextureBindings = 0;

        var nativeTextureBindings = chooseNativeTextureBindingCapacity();
        while (nativeTextureBindings > MaxFragmentTextureBindings)
        {
            var requestedDevice = requestDevice(nativeTextureBindings,
                requestPartiallyBoundTextureBindings);
            if (requestedDevice is not null)
            {
                requestedNativeTextureBindings = nativeTextureBindings;
                return requestedDevice;
            }

            if (nativeTextureBindings <= MaxFragmentTextureBindings * 2)
                break;

            nativeTextureBindings = Math.Max(MaxFragmentTextureBindings + 1, nativeTextureBindings / 2);
        }

        return null;
    }

    int chooseNativeTextureBindingCapacity()
    {
        SupportedLimits supportedLimits = default;
        wgpu.AdapterGetLimits(adapter, ref supportedLimits);

        var maxSampledTextures = supportedLimits.Limits.MaxSampledTexturesPerShaderStage;
        if (maxSampledTextures <= MaxFragmentTextureBindings || maxSampledTextures == uint.MaxValue)
            maxSampledTextures = MaxNativeFragmentTextureBindings;

        return Math.Max(1, (int)Math.Min(MaxNativeFragmentTextureBindings, maxSampledTextures));
    }

    bool supportsAdapterNativeNonUniformTextureIndexing()
        => wgpu.AdapterHasFeature(adapter,
               WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeatureTextureBindingArray)) &&
           wgpu.AdapterHasFeature(adapter,
               WebGpuNativeExtensions.NativeFeature(
                   WebGpuNativeExtensions.FeatureSampledTextureAndStorageBufferArrayNonUniformIndexing));

    bool supportsAdapterPartiallyBoundTextureBindingArrays()
        => wgpu.AdapterHasFeature(adapter,
            WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeaturePartiallyBoundBindingArray));

    bool deviceSupportsNativeNonUniformTextureIndexing(Device* requestedDevice)
        => wgpu.DeviceHasFeature(requestedDevice,
               WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeatureTextureBindingArray)) &&
           wgpu.DeviceHasFeature(requestedDevice,
               WebGpuNativeExtensions.NativeFeature(
                   WebGpuNativeExtensions.FeatureSampledTextureAndStorageBufferArrayNonUniformIndexing));

    Device* requestDevice(int nativeTextureBindingCapacity,
        bool requestPartiallyBoundTextureBindings = false)
    {
        using ManualResetEventSlim ready = new();
        Device* requestedDevice = null;
        var requestedFeatures = stackalloc FeatureName[3];
        var requestedFeatureCount = 0;
        if (nativeTextureBindingCapacity != 0)
        {
            requestedFeatures[requestedFeatureCount++] =
                WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeatureTextureBindingArray);
            requestedFeatures[requestedFeatureCount++] = WebGpuNativeExtensions.NativeFeature(
                WebGpuNativeExtensions.FeatureSampledTextureAndStorageBufferArrayNonUniformIndexing);
            if (requestPartiallyBoundTextureBindings)
                requestedFeatures[requestedFeatureCount++] =
                    WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeaturePartiallyBoundBindingArray);
        }

        var requiredLimits = createUndefinedRequiredLimits();
        if (nativeTextureBindingCapacity > MaxFragmentTextureBindings)
            requiredLimits.Limits.MaxSampledTexturesPerShaderStage = (uint)nativeTextureBindingCapacity;

        requestDeviceCallback = new PfnRequestDeviceCallback((status, receivedDevice, _, _) =>
        {
            if (status == RequestDeviceStatus.Success)
                requestedDevice = receivedDevice;
            ready.Set();
        });

        DeviceDescriptor descriptor = new()
        {
            DeviceLostCallback = deviceLostCallback,
            RequiredFeatureCount = (nuint)requestedFeatureCount,
            RequiredFeatures = requestedFeatureCount != 0 ? requestedFeatures : null,
            RequiredLimits = nativeTextureBindingCapacity > MaxFragmentTextureBindings ? &requiredLimits : null
        };

        wgpu.AdapterRequestDevice(adapter, in descriptor, requestDeviceCallback, null);
        ready.Wait();

        return requestedDevice;
    }

    static RequiredLimits createUndefinedRequiredLimits()
        => new()
        {
            Limits = new()
            {
                MaxTextureDimension1D = uint.MaxValue,
                MaxTextureDimension2D = uint.MaxValue,
                MaxTextureDimension3D = uint.MaxValue,
                MaxTextureArrayLayers = uint.MaxValue,
                MaxBindGroups = uint.MaxValue,
                MaxBindGroupsPlusVertexBuffers = uint.MaxValue,
                MaxBindingsPerBindGroup = uint.MaxValue,
                MaxDynamicUniformBuffersPerPipelineLayout = uint.MaxValue,
                MaxDynamicStorageBuffersPerPipelineLayout = uint.MaxValue,
                MaxSampledTexturesPerShaderStage = uint.MaxValue,
                MaxSamplersPerShaderStage = uint.MaxValue,
                MaxStorageBuffersPerShaderStage = uint.MaxValue,
                MaxStorageTexturesPerShaderStage = uint.MaxValue,
                MaxUniformBuffersPerShaderStage = uint.MaxValue,
                MaxUniformBufferBindingSize = ulong.MaxValue,
                MaxStorageBufferBindingSize = ulong.MaxValue,
                MinUniformBufferOffsetAlignment = uint.MaxValue,
                MinStorageBufferOffsetAlignment = uint.MaxValue,
                MaxVertexBuffers = uint.MaxValue,
                MaxBufferSize = ulong.MaxValue,
                MaxVertexAttributes = uint.MaxValue,
                MaxVertexBufferArrayStride = uint.MaxValue,
                MaxInterStageShaderComponents = uint.MaxValue,
                MaxInterStageShaderVariables = uint.MaxValue,
                MaxColorAttachments = uint.MaxValue,
                MaxColorAttachmentBytesPerSample = uint.MaxValue,
                MaxComputeWorkgroupStorageSize = uint.MaxValue,
                MaxComputeInvocationsPerWorkgroup = uint.MaxValue,
                MaxComputeWorkgroupSizeX = uint.MaxValue,
                MaxComputeWorkgroupSizeY = uint.MaxValue,
                MaxComputeWorkgroupSizeZ = uint.MaxValue,
                MaxComputeWorkgroupsPerDimension = uint.MaxValue
            }
        };

    void loadDeviceLimits()
    {
        SupportedLimits supportedLimits = default;
        wgpu.DeviceGetLimits(device, ref supportedLimits);

        var l = supportedLimits.Limits;
        var reportedMaxBufferSize = l.MaxBufferSize;
        if (reportedMaxBufferSize != 0)
            maxBufferSize = (int)Math.Min(reportedMaxBufferSize, int.MaxValue);

        if (UseNativeNonUniformTextureIndexing)
        {
            var nativeTextureBindings = l.MaxSampledTexturesPerShaderStage != 0 &&
                                        l.MaxSampledTexturesPerShaderStage != uint.MaxValue
                ? (int)Math.Min(MaxNativeFragmentTextureBindings, l.MaxSampledTexturesPerShaderStage)
                : MaxFragmentTextureBindings;
            maxFragmentTextureBindings = Math.Max(1, nativeTextureBindings);
        }
        else if (l.MaxSampledTexturesPerShaderStage != 0)
            maxFragmentTextureBindings = (int)Math.Min(l.MaxSampledTexturesPerShaderStage, MaxFragmentTextureBindings);
    }

    void rebuildCapabilities()
    {
        var features = GraphicsBackendFeatures.TextureAtlases |
            GraphicsBackendFeatures.Instancing |
            GraphicsBackendFeatures.ComputeShaders;
        if (UseNativeNonUniformTextureIndexing)
            features |= GraphicsBackendFeatures.NativeNonUniformTextureIndexing;

        Capabilities = new(features,
            16384,
            maxFragmentTextureBindings,
            0,
            0,
            maxFragmentTextureBindings,
            64 * 1024);
    }

    void logBackendCapabilities()
    {
        AdapterProperties properties = default;
        wgpu.AdapterGetProperties(adapter, ref properties);

        var name = SilkMarshal.PtrToString((nint)properties.Name) ?? "unknown";
        var vendor = SilkMarshal.PtrToString((nint)properties.VendorName) ?? "unknown";
        var driver = SilkMarshal.PtrToString((nint)properties.DriverDescription) ?? "unknown";
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU adapter: {name} ({vendor}); backend: {properties.BackendType}; type: {properties.AdapterType}; driver: {driver}");

        var featureCount = wgpu.DeviceEnumerateFeatures(device, null);
        if (featureCount != 0)
        {
            var features = stackalloc FeatureName[(int)featureCount];
            wgpu.DeviceEnumerateFeatures(device, features);

            var sb = new StringBuilder("WebGPU features:");
            for (nuint i = 0; i < featureCount; ++i)
                sb.Append(' ').Append(formatFeatureName(features[i]));
            SDL.LogInfo(LogCategory.Render, sb.ToString());
        }

        SupportedLimits supportedLimits = default;
        wgpu.DeviceGetLimits(device, ref supportedLimits);
        var l = supportedLimits.Limits;
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU limits: maxBufferSize={l.MaxBufferSize}; maxBindGroups={l.MaxBindGroups}; " +
            $"maxBindingsPerBindGroup={l.MaxBindingsPerBindGroup}; maxSampledTexturesPerStage={l.MaxSampledTexturesPerShaderStage}; " +
            $"maxSamplersPerStage={l.MaxSamplersPerShaderStage}; maxVertexBuffers={l.MaxVertexBuffers}; " +
            $"maxUniformBufferBindingSize={l.MaxUniformBufferBindingSize}; minUniformBufferOffsetAlignment={l.MinUniformBufferOffsetAlignment}");

        SDL.LogInfo(LogCategory.Render,
            UseNativeNonUniformTextureIndexing
                ? $"WebGPU texture binding mode: native array capacity={maxFragmentTextureBindings}; partiallyBound={nativeTextureBindingArraysPartiallyBound}"
                : $"WebGPU texture binding mode: core slots={maxFragmentTextureBindings}");
    }

    static string formatFeatureName(FeatureName feature)
    {
        if (feature == WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeatureTextureBindingArray))
            return "TextureBindingArray";
        if (feature == WebGpuNativeExtensions.NativeFeature(
                WebGpuNativeExtensions.FeatureSampledTextureAndStorageBufferArrayNonUniformIndexing))
            return "SampledTextureAndStorageBufferArrayNonUniformIndexing";
        if (feature == WebGpuNativeExtensions.NativeFeature(WebGpuNativeExtensions.FeaturePartiallyBoundBindingArray))
            return "PartiallyBoundBindingArray";

        return feature.ToString();
    }

    void initializeSurface()
    {
        wgpu.SurfaceGetCapabilities(surface, adapter, ref surfaceCapabilities);
        if (surfaceCapabilities.FormatCount == 0)
            throw new InvalidOperationException("WebGPU surface reported no supported formats");

        surfaceFormat = chooseSurfaceFormat();
        configureSurface();
        requestSurfaceTextureAcquire();
    }

    TextureFormat chooseSurfaceFormat()
    {
        if (supportsSurfaceFormat(TextureFormat.Bgra8Unorm))
            return TextureFormat.Bgra8Unorm;
        if (supportsSurfaceFormat(TextureFormat.Rgba8Unorm))
            return TextureFormat.Rgba8Unorm;
        if (supportsSurfaceFormat(TextureFormat.Bgra8UnormSrgb))
            return TextureFormat.Bgra8UnormSrgb;
        if (supportsSurfaceFormat(TextureFormat.Rgba8UnormSrgb))
            return TextureFormat.Rgba8UnormSrgb;

        return surfaceCapabilities.Formats[0];
    }

    bool supportsSurfaceFormat(TextureFormat format)
    {
        for (nuint i = 0; i < surfaceCapabilities.FormatCount; ++i)
            if (surfaceCapabilities.Formats[i] == format)
                return true;

        return false;
    }

    void configureSurface(bool force = false)
    {
        if (frameWidth == 0 || frameHeight == 0) return;

        var presentMode = choosePresentMode();
        var alphaMode = chooseCompositeAlphaMode();
        if (!force &&
            surfaceConfigured &&
            configuredSurfaceWidth == frameWidth &&
            configuredSurfaceHeight == frameHeight &&
            configuredSurfaceFormat == surfaceFormat &&
            configuredPresentMode == presentMode &&
            configuredAlphaMode == alphaMode)
            return;

        surfaceConfiguration = new()
        {
            Usage = TextureUsage.RenderAttachment,
            Format = surfaceFormat,
            PresentMode = presentMode,
            AlphaMode = alphaMode,
            Device = device,
            Width = frameWidth,
            Height = frameHeight
        };
        wgpu.SurfaceConfigure(surface, in surfaceConfiguration);
        surfaceConfigured = true;
        configuredSurfaceWidth = frameWidth;
        configuredSurfaceHeight = frameHeight;
        configuredSurfaceFormat = surfaceFormat;
        configuredPresentMode = presentMode;
        configuredAlphaMode = alphaMode;
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU surface: {frameWidth}x{frameHeight}; format={surfaceFormat}; present={presentMode}; alpha={alphaMode}");
    }

    PresentMode choosePresentMode()
    {
        if (!preferLowLatency || surfaceCapabilities.PresentModeCount == 0)
            return PresentMode.Fifo;

        if (supportsPresentMode(PresentMode.Mailbox))
            return PresentMode.Mailbox;
        if (supportsPresentMode(PresentMode.Immediate))
            return PresentMode.Immediate;
        if (supportsPresentMode(PresentMode.FifoRelaxed))
            return PresentMode.FifoRelaxed;
        return PresentMode.Fifo;
    }

    bool supportsPresentMode(PresentMode mode)
    {
        for (nuint i = 0; i < surfaceCapabilities.PresentModeCount; ++i)
            if (surfaceCapabilities.PresentModes[i] == mode)
                return true;

        return false;
    }

    CompositeAlphaMode chooseCompositeAlphaMode()
    {
        if (supportsCompositeAlphaMode(CompositeAlphaMode.Opaque))
            return CompositeAlphaMode.Opaque;
        if (supportsCompositeAlphaMode(CompositeAlphaMode.Auto))
            return CompositeAlphaMode.Auto;
        return surfaceCapabilities.AlphaModeCount != 0
            ? surfaceCapabilities.AlphaModes[0]
            : CompositeAlphaMode.Auto;
    }

    bool supportsCompositeAlphaMode(CompositeAlphaMode mode)
    {
        for (nuint i = 0; i < surfaceCapabilities.AlphaModeCount; ++i)
            if (surfaceCapabilities.AlphaModes[i] == mode)
                return true;

        return false;
    }

    static void onUncapturedError(ErrorType type, byte* message, void* userData)
    {
        var text = SilkMarshal.PtrToString((nint)message) ?? string.Empty;
        SDL.LogError(LogCategory.Render, $"WebGPU error: {type} {text}");
    }

    static NotImplementedException prototype()
        => new("The WebGPU backend is an opt-in prototype; only instance/device/surface setup is sketched in this pass.");
}
