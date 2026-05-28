namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using IO;
using SDL3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Util;
using Buffer = Ahjo.Wgpu.Buffer;

/// <summary>
/// Correctness-first WebGPU backend.
///
/// This rewrite intentionally has one owner for frame recording and one owner for queue submission.
/// There is no background submission queue, no replay thread, no staging-belt recall path and no
/// state snapshot cache. Every draw redundantly binds pipeline/state/resources, and every native
/// object released during or before a frame is fenced behind the next queue submission.
/// </summary>
public sealed class WebGpuGraphicsBackend : GraphicsBackendBase
{
    const int DefaultMaxFragmentTextures = 16;

    readonly Lock queueSync = new();
    readonly nint windowHandle;
    readonly WebGpuGraphicsDevice graphicsDevice;
    readonly WebGpuDeferredReleases deferredReleases;

    Adapter adapter;
    FrameState frame;
    Instance instance;
    int maxFragmentTextureBindings = DefaultMaxFragmentTextures;
    WebGpuSurfaceManager surfaceManager;

    public WebGpuGraphicsBackend(nint window = 0, bool preferLowLatency = false)
    {
        windowHandle = window;
        graphicsDevice = new(this);
        deferredReleases = new(this);

        Device = graphicsDevice;
        Buffers = new WebGpuGraphicsBufferFactory(this);
        RenderPipelines = new WebGpuRenderPipelineFactory(this);
        TextureFactory = new WebGpuTextureFactory(this);
        TextureUploader = new WebGpuAsyncTextureUploader(this);

        Capabilities = new(GraphicsBackendFeatures.TextureAtlases | GraphicsBackendFeatures.Instancing,
            16384,
            DefaultMaxFragmentTextures,
            0,
            0,
            DefaultMaxFragmentTextures,
            64 * 1024,
            4,
            0,
            8);
    }

    internal Device DeviceHandle { get; private set; }
    internal Queue QueueHandle => DeviceHandle?.Queue;
    internal WGPUTextureFormat SurfaceFormat => surfaceManager?.Format ?? WGPUTextureFormat.BGRA8Unorm;
    internal uint FramebufferWidth => surfaceManager?.Width ?? 0;
    internal uint FramebufferHeight => surfaceManager?.Height ?? 0;
    internal int MaxBufferSize { get; private set; } = 256 * 1024 * 1024;
    internal int MaxBindGroups { get; private set; } = 4;
    internal int MaxVertexBuffers { get; private set; } = 8;
    internal int MaxUniformBufferBindingSize { get; private set; } = 64 * 1024;
    internal int MinUniformBufferOffsetAlignment { get; private set; } = 256;
    internal int UniformBindingSize { get; private set; } = 256;
    internal int MaxBindingsPerBindGroup { get; private set; }
    internal bool HasActiveFrame => frame?.IsActive == true;
    internal bool IsDisposed { get; private set; }
    internal WebGpuDeferredReleases DeferredReleases => deferredReleases;

    public override string Name => "WebGPU";
    public override GraphicsBackendCapabilities Capabilities { get; protected set; }
    public override ShaderSourceLanguage ShaderSourceLanguage => ShaderSourceLanguage.Wgsl;
    public override IGraphicsDevice Device { get; }
    public override IGraphicsBufferFactory Buffers { get; }
    public override IRenderPipelineFactory RenderPipelines { get; }
    public override ITextureFactory TextureFactory { get; }
    public override IAsyncTextureUploader TextureUploader { get; }

    public override void Initialize(ResourceContainer resourceContainer, TextureContainer textureContainer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (instance is not null) return;

        instance = WebGpuInitializer.CreateInstance();
        if (windowHandle != 0)
            surfaceManager = new(this, instance, windowHandle);

        adapter = instance.RequestAdapterBlocking(surfaceManager?.Surface ?? default);
        DeviceHandle = adapter.RequestDeviceBlocking();

        loadDeviceLimits();
        surfaceManager?.Initialize(adapter);
        rebuildCapabilities();
        logBackendCapabilities();

        DrawState.Initialize(resourceContainer, textureContainer, this);
    }

    public override bool BeginFrame(Vector4 clearColor)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (frame?.IsActive == true)
            throw new InvalidOperationException("A WebGPU frame is already active");
        if (surfaceManager?.HasSurface != true)
            throw new NotSupportedException("The WebGPU backend needs an SDL window before it can render frames");

        DeviceHandle.ProcessEvents();
        deferredReleases.ReleaseCompleted();

        if (FramebufferWidth == 0 || FramebufferHeight == 0)
            return false;

        var surfaceFrame = surfaceManager.AcquireFrame(true);
        if (surfaceFrame is null)
            return false;

        frame ??= new(this);
        frame.Begin(surfaceFrame, surfaceManager.IsFormatSrgb ? SrgbColorSpace.ToLinear(clearColor) : clearColor);
        return true;
    }

    public override void EndFrame(bool discardFramebuffer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (frame?.IsActive != true) return;

        try
        {
            frame.Submit(discardFramebuffer);
        }
        finally
        {
            frame.DisposeAfterSubmit();
            deferredReleases.AfterQueueSubmit();
        }
    }

    public override IQuadRenderer CreateQuadRenderer() => new TexturedQuadRenderer(this);
    public override ILineRenderer CreateLineRenderer() => new LineRenderer(this);

    public override void Dispose()
    {
        if (IsDisposed) return;

        if (frame?.IsActive == true)
            EndFrame(true);

        IsDisposed = true;
        surfaceManager?.Dispose();
        surfaceManager = null;

        deferredReleases.WaitForAll();
        deferredReleases.Dispose();

        DeviceHandle?.Dispose();
        DeviceHandle = null;
        adapter?.Dispose();
        adapter = null;
        instance?.Dispose();
        instance = null;
    }

    public void Resize(uint width, uint height)
        => surfaceManager?.Resize(width, height);

    internal FrameState RequireFrame()
        => frame?.IsActive == true ? frame : throw new InvalidOperationException("No WebGPU frame is active");

    internal void RetainForFrame(object resource)
    {
        if (frame?.IsActive == true)
            frame.Retain(resource);
    }

    internal void RetireAfterSubmit(DeferredRelease resource)
    {
        if (resource.IsEmpty) return;
        if (frame?.IsActive == true)
            frame.RetireAfterSubmit(resource);
        else
            deferredReleases.Retire(resource);
    }

    internal void RetireAfterSubmit(BindGroup bindGroup) => RetireAfterSubmit(DeferredRelease.From(bindGroup));
    internal void RetireAfterSubmit(Buffer buffer) => RetireAfterSubmit(DeferredRelease.From(buffer));
    internal void RetireAfterSubmit(Texture texture) => RetireAfterSubmit(DeferredRelease.From(texture));
    internal void RetireAfterSubmit(TextureView textureView) => RetireAfterSubmit(DeferredRelease.From(textureView));
    internal void RetireAfterSubmit(Sampler sampler) => RetireAfterSubmit(DeferredRelease.From(sampler));

    internal void RetireAfterSubmit(IDisposable resource)
    {
        if (resource is null) return;
        RetireAfterSubmit(DeferredRelease.From(resource));
    }

    internal void RecordPipeline(RenderPipeline pipeline)
        => RequireFrame().Record(RenderPassCommand.SetPipeline(pipeline));

    internal void RecordBindGroup(uint slot, BindGroup bindGroup)
        => RequireFrame().Record(RenderPassCommand.SetBindGroup(slot, bindGroup));

    internal void RecordVertexBuffer(uint slot, Buffer buffer, ulong offset, ulong size)
        => RequireFrame().Record(RenderPassCommand.SetVertexBuffer(slot, buffer, offset, size));

    internal void RecordViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
        => RequireFrame().Record(RenderPassCommand.SetViewport(x, y, width, height, minDepth, maxDepth));

    internal void RecordScissorRect(uint x, uint y, uint width, uint height)
        => RequireFrame().Record(RenderPassCommand.SetScissorRect(x, y, width, height));

    internal void RecordDraw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
        => RequireFrame().Record(RenderPassCommand.Draw(vertexCount, instanceCount, firstVertex, firstInstance));

    internal void QueueWriteBuffer<T>(Buffer buffer, ulong offset, scoped ReadOnlySpan<T> data) where T : unmanaged
    {
        if (buffer.IsNull || data.IsEmpty) return;

        try
        {
            lock (queueSync)
                QueueHandle.WriteBuffer(buffer, offset, data);
        }
        catch (Exception ex)
        {
            throw WebGpuException.Fatal("writing to the WebGPU queue", ex);
        }
    }

    internal void QueueWriteTexture(Texture texture,
        scoped ReadOnlySpan<byte> data,
        int bytesPerRow,
        int rowsPerImage,
        WGPUOrigin3D origin,
        WGPUExtent3D extent)
    {
        if (texture.IsNull || data.IsEmpty) return;

        try
        {
            lock (queueSync)
                QueueHandle.WriteTexture(texture, data, (uint)bytesPerRow, (uint)rowsPerImage, in extent, 0, origin);
        }
        catch (Exception ex)
        {
            throw WebGpuException.Fatal($"writing {extent.width}x{extent.height} texture data to the WebGPU queue", ex);
        }
    }

    internal QueueWorkDoneRequest QueueOnSubmittedWorkDone()
    {
        try
        {
            lock (queueSync)
                return QueueHandle.BeginOnSubmittedWorkDone();
        }
        catch (Exception ex)
        {
            throw WebGpuException.Fatal("requesting WebGPU queue completion", ex);
        }
    }

    void SubmitCommandBuffer(CommandBuffer commandBuffer)
    {
        if (commandBuffer.IsNull) return;

        try
        {
            lock (queueSync)
                QueueHandle.Submit(commandBuffer);
        }
        catch (Exception ex)
        {
            throw WebGpuException.Fatal("submitting WebGPU command buffer", ex);
        }
    }

    void loadDeviceLimits()
    {
        var limits = DeviceHandle.GetLimits();
        if (limits.maxBufferSize != 0)
            MaxBufferSize = (int)Math.Min(limits.maxBufferSize, int.MaxValue);
        if (limits.maxBindGroups != 0)
            MaxBindGroups = (int)Math.Min(limits.maxBindGroups, int.MaxValue);
        if (limits.maxBindingsPerBindGroup != 0)
            MaxBindingsPerBindGroup = (int)Math.Min(limits.maxBindingsPerBindGroup, int.MaxValue);
        if (limits.maxVertexBuffers != 0)
            MaxVertexBuffers = (int)Math.Min(limits.maxVertexBuffers, int.MaxValue);
        if (limits.maxUniformBufferBindingSize != 0)
            MaxUniformBufferBindingSize = (int)Math.Min(limits.maxUniformBufferBindingSize, int.MaxValue);
        if (limits.minUniformBufferOffsetAlignment != 0)
            MinUniformBufferOffsetAlignment = (int)Math.Min(limits.minUniformBufferOffsetAlignment, int.MaxValue);

        UniformBindingSize = int.Min(MaxUniformBufferBindingSize, WebGpuResourceValidation.Align(256, MinUniformBufferOffsetAlignment));
        if (UniformBindingSize <= 0) UniformBindingSize = 256;

        var textureSlots = DefaultMaxFragmentTextures;
        if (limits.maxSampledTexturesPerShaderStage != 0)
            textureSlots = (int)Math.Min(textureSlots, limits.maxSampledTexturesPerShaderStage);
        if (limits.maxSamplersPerShaderStage != 0)
            textureSlots = (int)Math.Min(textureSlots, limits.maxSamplersPerShaderStage);
        if (limits.maxBindingsPerBindGroup != 0)
            textureSlots = (int)Math.Min(textureSlots, limits.maxBindingsPerBindGroup / 2);

        maxFragmentTextureBindings = int.Max(1, textureSlots);
    }

    void rebuildCapabilities()
    {
        var features = GraphicsBackendFeatures.TextureAtlases |
            GraphicsBackendFeatures.Instancing |
            GraphicsBackendFeatures.ComputeShaders;

        if (surfaceManager?.IsFormatSrgb == true)
            features |= GraphicsBackendFeatures.SrgbFramebuffer;

        Capabilities = new(features,
            16384,
            maxFragmentTextureBindings,
            0,
            0,
            maxFragmentTextureBindings,
            MaxUniformBufferBindingSize,
            MaxBindGroups,
            MaxBindingsPerBindGroup,
            MaxVertexBuffers);
    }

    void logBackendCapabilities()
    {
        var properties = adapter.GetInfo();
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU adapter: {properties.Device} ({properties.Vendor}); backend: {properties.Backend}; type: {properties.Type}; driver: {properties.Description}");

        var features = DeviceHandle.GetFeatures();
        if (features.Length != 0)
        {
            var sb = new StringBuilder("WebGPU features:");
            foreach (var feature in features)
                sb.Append(' ').Append(feature);
            SDL.LogInfo(LogCategory.Render, sb.ToString());
        }

        var limits = DeviceHandle.GetLimits();
        SDL.LogInfo(LogCategory.Render,
            $"WebGPU limits: maxBufferSize={limits.maxBufferSize}; maxBindGroups={limits.maxBindGroups}; " +
            $"maxBindingsPerBindGroup={limits.maxBindingsPerBindGroup}; maxSampledTexturesPerStage={limits.maxSampledTexturesPerShaderStage}; " +
            $"maxSamplersPerStage={limits.maxSamplersPerShaderStage}; maxVertexBuffers={limits.maxVertexBuffers}; " +
            $"maxUniformBufferBindingSize={limits.maxUniformBufferBindingSize}; minUniformBufferOffsetAlignment={limits.minUniformBufferOffsetAlignment}");
    }

    internal sealed class FrameState : IDisposable
    {
        readonly WebGpuGraphicsBackend backend;
        readonly List<RenderPassCommand> commands = [];
        readonly List<object> retained = [];
        readonly List<DeferredRelease> retireAfterSubmit = [];
        Vector4 clearColor;
        bool submitted;

        public FrameState(WebGpuGraphicsBackend backend)
            => this.backend = backend;

        public bool IsActive { get; private set; }
        public WebGpuSurfaceFrame SurfaceFrame { get; private set; }

        public void Begin(WebGpuSurfaceFrame surfaceFrame, Vector4 clearColor)
        {
            if (IsActive)
                throw new InvalidOperationException("A WebGPU frame is already active");

            SurfaceFrame = surfaceFrame;
            this.clearColor = clearColor;
            submitted = false;
            IsActive = true;
        }

        public void Record(RenderPassCommand command)
            => commands.Add(command);

        public void Submit(bool discardFramebuffer)
        {
            if (!IsActive || submitted) return;
            submitted = true;

            CommandEncoder encoder = default;
            RenderPassEncoder pass = default;
            CommandBuffer commandBuffer = default;
            var passEnded = false;
            var surfaceFinished = false;

            try
            {
                encoder = backend.DeviceHandle.CreateCommandEncoder();
                Span<ColorAttachment> attachments = stackalloc ColorAttachment[1];
                attachments[0] = new(SurfaceFrame.TextureView,
                    WGPULoadOp.Clear,
                    WGPUStoreOp.Store,
                    new()
                    {
                        r = clearColor.X,
                        g = clearColor.Y,
                        b = clearColor.Z,
                        a = clearColor.W
                    });

                pass = encoder.BeginRenderPass(attachments);

                foreach (var command in commands)
                    command.Replay(pass);

                pass.Dispose();
                passEnded = true;

                commandBuffer = encoder.Finish();
                backend.SubmitCommandBuffer(commandBuffer);

                if (!discardFramebuffer)
                    SurfaceFrame.Present();
                else
                    SurfaceFrame.Dispose();
                surfaceFinished = true;
            }
            finally
            {
                if (!passEnded)
                    pass.Dispose();
                commandBuffer.Dispose();
                encoder.Dispose();
                if (!surfaceFinished)
                    SurfaceFrame.Dispose();
            }
        }

        public void Retain(object resource)
        {
            if (resource is not null)
                retained.Add(resource);
        }

        public void RetireAfterSubmit(DeferredRelease resource)
        {
            if (!resource.IsEmpty)
                retireAfterSubmit.Add(resource);
        }

        public void DisposeAfterSubmit()
        {
            try
            {
                foreach (var resource in retireAfterSubmit)
                    backend.DeferredReleases.Retire(resource);
            }
            finally
            {
                commands.Clear();
                retireAfterSubmit.Clear();
                retained.Clear();
                SurfaceFrame = null;
                IsActive = false;
                submitted = false;
            }
        }

        public void Dispose() => DisposeAfterSubmit();
    }

    internal readonly struct RenderPassCommand
    {
        readonly RenderPassCommandKind kind;
        readonly RenderPipeline pipeline;
        readonly BindGroup bindGroup;
        readonly Buffer buffer;
        readonly uint slot;
        readonly uint x;
        readonly uint y;
        readonly uint width;
        readonly uint height;
        readonly uint vertexCount;
        readonly uint instanceCount;
        readonly uint firstVertex;
        readonly uint firstInstance;
        readonly ulong offset;
        readonly ulong size;
        readonly float fx;
        readonly float fy;
        readonly float fwidth;
        readonly float fheight;
        readonly float minDepth;
        readonly float maxDepth;

        RenderPassCommand(RenderPassCommandKind kind,
            RenderPipeline pipeline = default,
            BindGroup bindGroup = default,
            Buffer buffer = default,
            uint slot = 0,
            uint x = 0,
            uint y = 0,
            uint width = 0,
            uint height = 0,
            uint vertexCount = 0,
            uint instanceCount = 0,
            uint firstVertex = 0,
            uint firstInstance = 0,
            ulong offset = 0,
            ulong size = 0,
            float fx = 0,
            float fy = 0,
            float fwidth = 0,
            float fheight = 0,
            float minDepth = 0,
            float maxDepth = 0)
        {
            this.kind = kind;
            this.pipeline = pipeline;
            this.bindGroup = bindGroup;
            this.buffer = buffer;
            this.slot = slot;
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.vertexCount = vertexCount;
            this.instanceCount = instanceCount;
            this.firstVertex = firstVertex;
            this.firstInstance = firstInstance;
            this.offset = offset;
            this.size = size;
            this.fx = fx;
            this.fy = fy;
            this.fwidth = fwidth;
            this.fheight = fheight;
            this.minDepth = minDepth;
            this.maxDepth = maxDepth;
        }

        public static RenderPassCommand SetPipeline(RenderPipeline pipeline)
            => new(RenderPassCommandKind.SetPipeline, pipeline: pipeline);

        public static RenderPassCommand SetBindGroup(uint slot, BindGroup bindGroup)
            => new(RenderPassCommandKind.SetBindGroup, bindGroup: bindGroup, slot: slot);

        public static RenderPassCommand SetVertexBuffer(uint slot, Buffer buffer, ulong offset, ulong size)
            => new(RenderPassCommandKind.SetVertexBuffer, buffer: buffer, slot: slot, offset: offset, size: size);

        public static RenderPassCommand SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
            => new(RenderPassCommandKind.SetViewport, fx: x, fy: y, fwidth: width, fheight: height, minDepth: minDepth, maxDepth: maxDepth);

        public static RenderPassCommand SetScissorRect(uint x, uint y, uint width, uint height)
            => new(RenderPassCommandKind.SetScissorRect, x: x, y: y, width: width, height: height);

        public static RenderPassCommand Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
            => new(RenderPassCommandKind.Draw, vertexCount: vertexCount, instanceCount: instanceCount, firstVertex: firstVertex, firstInstance: firstInstance);

        public void Replay(RenderPassEncoder pass)
        {
            switch (kind)
            {
                case RenderPassCommandKind.SetPipeline:
                    pass.SetPipeline(pipeline);
                    return;
                case RenderPassCommandKind.SetBindGroup:
                    pass.SetBindGroup(slot, bindGroup);
                    return;
                case RenderPassCommandKind.SetVertexBuffer:
                    pass.SetVertexBuffer(slot, buffer, offset, size);
                    return;
                case RenderPassCommandKind.SetViewport:
                    pass.SetViewport(fx, fy, fwidth, fheight, minDepth, maxDepth);
                    return;
                case RenderPassCommandKind.SetScissorRect:
                    pass.SetScissorRect(x, y, width, height);
                    return;
                case RenderPassCommandKind.Draw:
                    pass.Draw(vertexCount, instanceCount, firstVertex, firstInstance);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }

    enum RenderPassCommandKind
    {
        SetPipeline,
        SetBindGroup,
        SetVertexBuffer,
        SetViewport,
        SetScissorRect,
        Draw
    }
}

sealed class WebGpuGraphicsDevice(WebGpuGraphicsBackend backend) : IGraphicsDevice
{
    Rectangle? scissor;
    Rectangle viewport;

    public BlendingFactorState BlendState { get; private set; } = new(BlendingMode.AlphaBlend);

    public void InitializeTextureSlots(int textureSlotCount) { }
    public void ResetStateCache() { }

    public void SetViewport(Rectangle viewport)
    {
        this.viewport = viewport;
        if (viewport.Width > 0 && viewport.Height > 0)
            backend.Resize((uint)viewport.Width, (uint)viewport.Height);
    }

    public void SetScissor(Rectangle? region) => scissor = region;
    public void SetCapability(GraphicsCapability capability, bool enabled) { }
    public void SetBlendState(BlendingFactorState state) => BlendState = state;
    public void UseProgram(int programId) { }
    public void ActivateVertexAttributes(VertexDeclaration declaration, Shader shader) => throw prototype();
    public void DeactivateVertexAttributes(VertexDeclaration declaration, Shader shader) => throw prototype();
    public int BindTexture(ITexture texture) => throw prototype();
    public void BindTextures(scoped ReadOnlySpan<ITexture> textures, Span<int> textureUnits) => throw prototype();
    public void UnbindTexture(ITexture texture) { }
    public void Dispose() { }

    internal void RecordState()
    {
        recordViewport();
        recordScissor();
    }

    void recordViewport()
    {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        backend.RecordViewport(viewport.X, toNativeY(viewport), viewport.Width, viewport.Height, 0, 1);
    }

    void recordScissor()
    {
        var region = scissor ?? viewport;
        if (!tryGetNativeScissor(region, out var x, out var y, out var width, out var height))
        {
            if (scissor.HasValue)
                backend.RecordScissorRect(0, 0, 0, 0);
            return;
        }

        backend.RecordScissorRect((uint)x, (uint)y, (uint)width, (uint)height);
    }

    int toNativeY(Rectangle region)
    {
        var height = backend.FramebufferHeight != 0 && backend.FramebufferHeight <= int.MaxValue
            ? (int)backend.FramebufferHeight
            : viewport.Height;
        return height - region.Y - region.Height;
    }

    bool tryGetNativeScissor(Rectangle region, out int x, out int y, out int width, out int height)
    {
        var boundsWidth = backend.FramebufferWidth != 0 && backend.FramebufferWidth <= int.MaxValue
            ? (int)backend.FramebufferWidth
            : viewport.Width;
        var boundsHeight = backend.FramebufferHeight != 0 && backend.FramebufferHeight <= int.MaxValue
            ? (int)backend.FramebufferHeight
            : viewport.Height;

        var left = Math.Clamp(region.X, 0, boundsWidth);
        var top = Math.Clamp(boundsHeight - region.Y - region.Height, 0, boundsHeight);
        var right = Math.Clamp(region.X + region.Width, 0, boundsWidth);
        var bottom = Math.Clamp(boundsHeight - region.Y, 0, boundsHeight);

        x = left;
        y = top;
        width = right - left;
        height = bottom - top;
        return width > 0 && height > 0;
    }

    static NotImplementedException prototype()
        => new("WebGPU uses the render-pipeline path; the legacy shader/device path is unavailable.");
}
