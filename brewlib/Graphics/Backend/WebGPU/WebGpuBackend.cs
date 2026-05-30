namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using IO;
using Renderers;
using SDL3;
using Shaders;
using SixLabors.ImageSharp;
using Textures;
using ZLinq;
using Surface = Ahjo.Wgpu.Surface;

public sealed class WebGpuBackend : GraphicsBackendBase
{
    readonly List<WebGpuRenderPipeline> pipelines = new();
    readonly HashSet<WebGpuGraphicsBuffer> ringBuffers = new(ReferenceEqualityComparer.Instance);
    readonly IntPtr sdlWindow;
    WebGpuBufferFactory bufferFactory;
    WebGpuUniformRing uniformRing;

    WGPUColor clear;

    WebGpuDevice device;
    WebGpuDeviceContext deviceContext;

    bool disposed;
    WebGpuFrameContext frameContext;

    bool hasViewport;

    Instance instance;

    Rectangle? lastScissorRegion;
    WebGpuRenderPipelineFactory pipelineFactory;
    bool redirectingInitializeToDrawState;
    WebGpuSamplerCache samplerCache;
    uint scX, scY, scW, scH;
    WebGpuSurfaceContext surfaceContext;
    WebGpuTextureFactory textureFactory;
    WebGpuAsyncTextureUploader textureUploader;
    float vpX, vpY, vpW, vpH;

    public WebGpuBackend(IntPtr sdlWindow)
    {
        if (sdlWindow == IntPtr.Zero)
            throw new ArgumentException("A valid SDL window handle is required", nameof(sdlWindow));

        this.sdlWindow = sdlWindow;
        InitializeGpu();
    }

    public override string Name => "WebGPU";
    public override ShaderSourceLanguage ShaderSourceLanguage => ShaderSourceLanguage.Wgsl;

    public override GraphicsBackendCapabilities Capabilities { get; protected set; }

    public override IGraphicsDevice Device => device;
    public override IGraphicsBufferFactory Buffers => bufferFactory;
    public override IRenderPipelineFactory RenderPipelines => pipelineFactory;
    public override ITextureFactory TextureFactory => textureFactory;
    public override IAsyncTextureUploader TextureUploader => textureUploader;

    internal bool IsFrameActive => frameContext is not null && frameContext.IsFrameActive;
    internal WebGpuFrameRecorder Recorder { get; private set; }

    internal WebGpuUniformRing UniformRing => uniformRing;

    internal WGPUTextureFormat SurfaceFormat { get; private set; }

    internal BlendingFactorState CurrentBlendState { get; private set; } = new(BlendingMode.AlphaBlend);

    internal bool ScissorEnabled { get; private set; }

    public override void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        if (ReferenceEquals(DrawState.Backend, this)) return;

        if (DrawState.Backend is not null)
            throw new InvalidOperationException($"Cannot initialize {Name}: DrawState is already bound to {DrawState.Backend.Name}");

        if (redirectingInitializeToDrawState)
            return;

        redirectingInitializeToDrawState = true;
        try
        {
            DrawState.Initialize(resourceContainer, textureContainer, this);
        }
        finally
        {
            redirectingInitializeToDrawState = false;
        }
    }

    public override IQuadRenderer CreateQuadRenderer() => new TexturedQuadRenderer(this);
    public override ILineRenderer CreateLineRenderer() => new LineRenderer(this);

    internal void EnqueueDeferredDisposal(Ahjo.Wgpu.Buffer buffer) => frameContext?.EnqueueDeferred(buffer);
    internal void EnqueueDeferredDisposal(BindGroup bindGroup) => frameContext?.EnqueueDeferred(bindGroup);

    internal void StageBufferWrite(Ahjo.Wgpu.Buffer buffer, ulong offset, scoped ReadOnlySpan<byte> data)
        => StageBufferWrite(buffer, offset, data, data.Length);

    internal void StageBufferWrite(Ahjo.Wgpu.Buffer buffer, ulong offset, scoped ReadOnlySpan<byte> data, int paddedLength)
    {
        if (frameContext is not null && frameContext.IsFrameActive)
        {
            frameContext.StageBufferWrite(buffer, offset, data, paddedLength);
            return;
        }

        if (paddedLength == data.Length)
        {
            deviceContext.Queue.WriteBuffer(buffer, offset, data);
            return;
        }

        using var owner = Configuration.Default.MemoryAllocator.Allocate<byte>(paddedLength);
        var padded = owner.Memory.Span.Slice(0, paddedLength);
        data.CopyTo(padded);
        padded.Slice(data.Length).Clear();
        deviceContext.Queue.WriteBuffer(buffer, offset, padded);
    }

    internal void RegisterPipeline(WebGpuRenderPipeline pipeline) => pipelines.Add(pipeline);
    internal void UnregisterPipeline(WebGpuRenderPipeline pipeline) => pipelines.Remove(pipeline);

    internal void RegisterRingBuffer(WebGpuGraphicsBuffer buffer) => ringBuffers.Add(buffer);
    internal void UnregisterRingBuffer(WebGpuGraphicsBuffer buffer) => ringBuffers.Remove(buffer);

    internal bool TryGetViewport(out float x, out float y, out float w, out float h)
    {
        x = vpX;
        y = vpY;
        w = vpW;
        h = vpH;
        return hasViewport;
    }

    internal bool TryGetScissor(out uint x, out uint y, out uint w, out uint h)
    {
        x = scX;
        y = scY;
        w = scW;
        h = scH;
        return ScissorEnabled;
    }

    internal void SetViewport(Rectangle viewport)
    {
        hasViewport = true;
        vpX = viewport.X;
        vpY = viewport.Y;
        vpW = Math.Max(0f, viewport.Width);
        vpH = Math.Max(0f, viewport.Height);

        if (lastScissorRegion.HasValue)
            SetScissor(lastScissorRegion);
    }

    internal void SetScissor(Rectangle? region)
    {
        lastScissorRegion = region;
        if (region is not { } r)
        {
            ScissorEnabled = false;
            return;
        }

        var boundsWidth = surfaceContext is not null && surfaceContext.Width != 0 ? surfaceContext.Width > int.MaxValue ? int.MaxValue : (int)surfaceContext.Width :
            hasViewport ? (int)Math.Max(0f, vpW) : r.Right;

        var boundsHeight = surfaceContext is not null && surfaceContext.Height != 0 ? surfaceContext.Height > int.MaxValue ? int.MaxValue : (int)surfaceContext.Height :
            hasViewport ? (int)Math.Max(0f, vpH) : r.Bottom;

        var left = r.X;
        var top = boundsHeight - r.Y - r.Height;
        var right = r.Right;
        var bottom = top + r.Height;

        left = Math.Clamp(left, 0, boundsWidth);
        right = Math.Clamp(right, 0, boundsWidth);
        top = Math.Clamp(top, 0, boundsHeight);
        bottom = Math.Clamp(bottom, 0, boundsHeight);

        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0)
        {
            ScissorEnabled = false;
            return;
        }

        ScissorEnabled = true;
        scX = (uint)left;
        scY = (uint)top;
        scW = (uint)width;
        scH = (uint)height;
    }

    internal void SetBlendState(BlendingFactorState state) => CurrentBlendState = state;
    internal void ResetStateCache() => CurrentBlendState = new(BlendingMode.AlphaBlend);

    public override bool BeginFrame(Vector4 clearColor)
    {
        if (IsFrameActive)
            throw new InvalidOperationException("BeginFrame called while a frame was already active");

        instance.ProcessEvents();
        if (!surfaceContext.BeginFrame())
            return false;

        frameContext.BeginFrame();
        Recorder.Reset();

        uniformRing.ResetFrame();
        foreach (var buffer in ringBuffers) buffer.ResetFrame();
        foreach (var pipeline in pipelines) pipeline.OnFrameBegin();

        clear = new()
        {
            r = clearColor.X,
            g = clearColor.Y,
            b = clearColor.Z,
            a = clearColor.W
        };

        return true;
    }

    public override void EndFrame(bool discardFramebuffer)
    {
        if (!IsFrameActive) return;

        if (!surfaceContext.AcquireFrame())
        {
            Recorder.Reset();
            frameContext.AbortFrame();
            return;
        }

        try
        {
            using var encoder = deviceContext.Device.CreateCommandEncoder();

            if (frameContext.FlushUploads(encoder))
                frameContext.FinishUploads();

            Recorder.Replay(encoder,
                surfaceContext.CurrentView,
                in clear,
                surfaceContext.Width,
                surfaceContext.Height);

            using var cmd = encoder.Finish();
            deviceContext.Queue.Submit(cmd);

            surfaceContext.EndFrame(discardFramebuffer);
            frameContext.EndFrame();
        }
        catch
        {
            surfaceContext.AbortFrame();
            frameContext.AbortFrame();
            throw;
        }
    }

    void InitializeGpu()
    {
        Surface surface;
        Adapter adapter;
        Device wgpuDevice;

        var preferredBackends = ChoosePreferredBackends();
        try
        {
            CreateInstanceSurfaceAdapter(preferredBackends, out surface, out adapter);
        }
        catch when (!EqualityComparer<InstanceBackends>.Default.Equals(preferredBackends, InstanceBackends.Primary))
        {
            SDL.LogInfo(LogCategory.Render, "WebGPU preferred (Vulkan) adapter unavailable; falling back to default backend selection");
            CreateInstanceSurfaceAdapter(InstanceBackends.Primary, out surface, out adapter);
        }

        var caps = surface.GetCapabilities(adapter);
        var wantSrgb = DrawState.UseSrgb;
        SurfaceFormat = ChooseSurfaceFormat(caps, wantSrgb, out var srgbFramebuffer, out var manualColorCorrection);

        var requestedCapabilities = WebGpuDeviceCapabilities.Build(adapter);
        var deviceDesc = new DeviceDescriptor
        {
            RequiredFeatures = requestedCapabilities.RequiredFeatures,
            RequiredLimits = requestedCapabilities.RequiredLimits,
            RequiredNativeLimits = requestedCapabilities.RequiredNativeLimits
        };

        try
        {
            wgpuDevice = adapter.RequestDeviceBlocking(in deviceDesc);
        }
        catch
        {
            surface.Dispose();
            adapter.Dispose();
            instance.Dispose();
            throw;
        }

        var deviceCapabilities = requestedCapabilities.ResolveForCreatedDevice(wgpuDevice);
        var deviceLimits = deviceCapabilities.RequiredLimits;
        var nativeLimits = deviceCapabilities.NativeLimits;

        SDL.LogInfo(LogCategory.Render,
            deviceCapabilities.FormatLog(requestedCapabilities.RequiredLimits,
                SurfaceFormat,
                srgbFramebuffer,
                manualColorCorrection));

        deviceContext = new(instance,
            adapter,
            wgpuDevice,
            deviceLimits,
            nativeLimits,
            deviceCapabilities.HasImmediates,
            deviceCapabilities.HasTextureBindingArray,
            deviceCapabilities.HasNonUniformIndexing,
            deviceCapabilities.HasMultiDrawIndirect,
            deviceCapabilities.HasBcCompression);

        const WGPUPresentMode presentMode = WGPUPresentMode.Fifo;
        var alphaMode = caps.SupportsAlphaMode(WGPUCompositeAlphaMode.Opaque)
            ? WGPUCompositeAlphaMode.Opaque
            : WGPUCompositeAlphaMode.Auto;

        surfaceContext = new(sdlWindow, surface, wgpuDevice, adapter, SurfaceFormat, presentMode, alphaMode);
        surfaceContext.Configure();

        frameContext = new(deviceContext);
        Recorder = new();

        samplerCache = new(wgpuDevice, Name);
        device = new(this);
        bufferFactory = new(this, deviceContext);
        uniformRing = new(this, deviceContext);
        pipelineFactory = new(this, deviceContext);
        textureFactory = new(deviceContext, samplerCache, this);
        textureUploader = new(this,
            textureFactory,
            deviceContext,
            () => (int)deviceLimits.maxTextureDimension2D);

        // The device grants exactly the binding-array element count we requested (0 when the
        // feature/limit wasn't requested). Use that, not the adapter's raw native report, so the
        // public cap matches what the device will actually accept in a layout.
        var grantedArrayElements = deviceCapabilities.HasTextureBindingArray
            ? deviceCapabilities.RequiredNativeLimits?.maxBindingArrayElementsPerShaderStage ?? 0u
            : 0u;

        Capabilities = BuildCapabilities(deviceLimits,
            grantedArrayElements,
            srgbFramebuffer,
            manualColorCorrection,
            deviceCapabilities.HasNonUniformIndexing || deviceCapabilities.HasTextureBindingArray);
    }


    void CreateInstanceSurfaceAdapter(InstanceBackends backends, out Surface surface, out Adapter adapter)
    {
        var instanceDesc = new InstanceDescriptor
        {
            Flags = InstanceFlags.None,
            Backends = backends
        };

        instance = Instance.Create(in instanceDesc);
        try
        {
            surface = instance.CreateSurface(SdlSurfaceFactory.Create(sdlWindow));
            adapter = instance.RequestAdapterBlocking(surface);
        }
        catch
        {
            instance.Dispose();
            instance = null;
            throw;
        }
    }

    static InstanceBackends ChoosePreferredBackends()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(10)) return InstanceBackends.Dx12;
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS()) return InstanceBackends.Metal;
        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid()) return InstanceBackends.Vulkan;
        return InstanceBackends.GL;
    }

    static WGPUTextureFormat ChooseSurfaceFormat(SurfaceCapabilities caps,
        bool wantSrgb,
        out bool srgbFramebuffer,
        out bool manualColorCorrection)
    {
        srgbFramebuffer = false;
        manualColorCorrection = false;

        if (wantSrgb)
        {
            if (caps.SupportsFormat(WGPUTextureFormat.BGRA8UnormSrgb))
            {
                srgbFramebuffer = true;
                return WGPUTextureFormat.BGRA8UnormSrgb;
            }

            if (caps.SupportsFormat(WGPUTextureFormat.RGBA8UnormSrgb))
            {
                srgbFramebuffer = true;
                return WGPUTextureFormat.RGBA8UnormSrgb;
            }

            manualColorCorrection = true;
        }

        if (caps.SupportsFormat(WGPUTextureFormat.BGRA8Unorm)) return WGPUTextureFormat.BGRA8Unorm;
        if (caps.SupportsFormat(WGPUTextureFormat.RGBA8Unorm)) return WGPUTextureFormat.RGBA8Unorm;

        return caps.Formats.Length > 0 ? caps.Formats[0] : WGPUTextureFormat.BGRA8Unorm;
    }

    static GraphicsBackendCapabilities BuildCapabilities(WGPULimits limits,
        uint grantedArrayElements,
        bool srgbFramebuffer,
        bool manualColorCorrection,
        bool nonUniformIndexing)
    {
        var features = GraphicsBackendFeatures.Instancing
            | GraphicsBackendFeatures.TextureAtlases
            | GraphicsBackendFeatures.ClearTexture;

        if (srgbFramebuffer) features |= GraphicsBackendFeatures.SrgbFramebuffer;
        if (manualColorCorrection) features |= GraphicsBackendFeatures.ManualColorCorrection;
        if (nonUniformIndexing)
            features |= GraphicsBackendFeatures.NonUniformTextureIndexing | GraphicsBackendFeatures.NativeNonUniformTextureIndexing;

        static int Clamp(ulong value) => value > int.MaxValue ? int.MaxValue : (int)value;
        var sampledPerStage = (int)limits.maxSampledTexturesPerShaderStage;

        // Already capped to the descriptor budget at request time; clamp defensively in case the
        // device somehow reports more than asked. 0 means binding arrays weren't granted.
        var arrayElements = Math.Min(Clamp(grantedArrayElements),
            WebGpuDeviceCapabilities.TextureArrayElementCeiling);

        return new(
            features,
            (int)limits.maxTextureDimension2D,
            sampledPerStage,
            sampledPerStage,
            0,
            sampledPerStage,
            Clamp(limits.maxUniformBufferBindingSize),
            (int)limits.maxBindGroups,
            (int)limits.maxBindingsPerBindGroup,
            (int)limits.maxVertexBuffers,
            arrayElements);
    }

    public override void Dispose()
    {
        if (disposed) return;

        disposed = true;

        using (var pipelinesSnapshot = pipelines.AsValueEnumerable().ToArrayPool())
            foreach (var pipeline in pipelinesSnapshot.Span) 
                pipeline.Dispose();

        pipelines.Clear();
        ringBuffers.Clear();

        uniformRing?.Dispose();

        textureUploader?.Dispose();
        samplerCache?.Dispose();
        device?.Dispose();
        frameContext?.Dispose();
        surfaceContext?.Dispose();
        deviceContext?.Dispose();
        instance?.Dispose();
    }
}