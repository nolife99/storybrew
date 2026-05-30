namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using System.Text;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using Shaders;
using WgpuVertexAttribute = Ahjo.Wgpu.VertexAttribute;
using WgpuVertexBufferLayout = Ahjo.Wgpu.VertexBufferLayout;

sealed class WebGpuRenderPipeline : IRenderPipeline
{
    readonly WebGpuBackend backend;
    readonly WebGpuBindGroupCache bindCache;
    readonly WebGpuDeviceContext deviceContext;
    readonly BindGroupLayout emptyGroup0Layout;
    readonly byte[] fragmentEntryUtf8;
    readonly ShaderModule fragmentModule;
    readonly bool hasTextureGroup;
    readonly bool ownsEmptyLayout;
    readonly bool ownsTextureLayout;
    readonly bool ownsUniformLayout;
    readonly BindGroupLayout textureBindLayout;
    readonly BindGroupLayout uniformBindLayout;

    readonly Dictionary<BlendingFactorState, RenderPipeline> variants = new();
    readonly byte[] vertexEntryUtf8;

    readonly ShaderModule vertexModule;

    readonly WgpuVertexLayoutPlan vertexPlan;

    readonly PipelineLayout wgpuPipelineLayout;

    bool disposed;

    public WebGpuRenderPipeline(WebGpuBackend backend,
        WebGpuDeviceContext deviceContext,
        RenderPipelineDescription description,
        ShaderModule vertexModule,
        ShaderModule fragmentModule,
        string vertexEntry,
        string fragmentEntry,
        PipelineLayout wgpuPipelineLayout,
        BindGroupLayout textureBindLayout,
        bool hasTextureGroup,
        bool ownsTextureLayout,
        int textureSlotCount,
        bool textureArrayed,
        BindGroupLayout uniformBindLayout,
        bool ownsUniformLayout,
        BindGroupLayout emptyGroup0Layout,
        bool ownsEmptyLayout,
        uint textureGroupIndex,
        uint uniformGroupIndex,
        bool hasUniformGroup,
        WgpuVertexLayoutPlan vertexPlan,
        WebGpuUniformState uniformState)
    {
        this.backend = backend;
        this.deviceContext = deviceContext;
        Description = description;
        this.vertexModule = vertexModule;
        this.fragmentModule = fragmentModule;
        vertexEntryUtf8 = Encoding.UTF8.GetBytes(vertexEntry);
        fragmentEntryUtf8 = Encoding.UTF8.GetBytes(fragmentEntry);
        this.wgpuPipelineLayout = wgpuPipelineLayout;
        this.textureBindLayout = textureBindLayout;
        this.uniformBindLayout = uniformBindLayout;
        this.emptyGroup0Layout = emptyGroup0Layout;
        this.hasTextureGroup = hasTextureGroup;
        this.ownsTextureLayout = ownsTextureLayout;
        this.ownsUniformLayout = ownsUniformLayout;
        this.ownsEmptyLayout = ownsEmptyLayout;
        this.vertexPlan = vertexPlan;
        UniformState = uniformState;

        bindCache = new(backend,
            deviceContext.Device,
            textureBindLayout,
            textureGroupIndex,
            hasTextureGroup,
            textureSlotCount,
            textureArrayed,
            uniformBindLayout,
            uniformGroupIndex,
            hasUniformGroup);
    }

    public WebGpuUniformState UniformState { get; }

    public RenderPipelineDescription Description { get; }

    public IRenderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name) where T : struct
    {
        return new WebGpuRenderUniform<T>(UniformState);
    }

    public IRenderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform) where T : struct
        => GetUniform<T>(uniform.Name.AsSpan());

    public IResourceSet CreateResourceSet() => new WebGpuResourceSet(this, bindCache);

    public void Draw(DrawCommand command, scoped ReadOnlySpan<RenderVertexBufferBinding> vertexBuffers, IResourceSet resources = null)
    {
        if (command.VertexCount <= 0) return;

        EncodeDraw(vertexBuffers, resources, 1, command.VertexCount, command.FirstVertex);
    }

    public void DrawInstanced(DrawInstancedCommand command, scoped ReadOnlySpan<RenderVertexBufferBinding> vertexBuffers, IResourceSet resources = null)
    {
        if (command.VertexCount <= 0 || command.InstanceCount <= 0) return;

        EncodeDraw(vertexBuffers, resources, command.InstanceCount, command.VertexCount, command.FirstVertex);
    }

    public void Dispose()
    {
        if (disposed) return;

        disposed = true;

        backend.UnregisterPipeline(this);

        bindCache.Dispose();

        foreach (var variant in variants.Values) variant.Dispose();
        variants.Clear();

        wgpuPipelineLayout.Dispose();
        if (ownsTextureLayout && !textureBindLayout.IsNull) textureBindLayout.Dispose();
        if (ownsUniformLayout && !uniformBindLayout.IsNull) uniformBindLayout.Dispose();
        if (ownsEmptyLayout && !emptyGroup0Layout.IsNull) emptyGroup0Layout.Dispose();

        vertexModule.Dispose();
        if (!fragmentModule.IsNull) fragmentModule.Dispose();
    }

    public void OnFrameBegin() => UniformState.BeginFrame();

    void EncodeDraw(scoped ReadOnlySpan<RenderVertexBufferBinding> vertexBuffers,
        IResourceSet resources,
        int instanceCount,
        int vertexCount,
        int firstVertex)
    {
        if (!backend.IsFrameActive)
            throw new InvalidOperationException("Draw called outside of BeginFrame/EndFrame");

        var blend = backend.CurrentBlendState;
        if (!variants.TryGetValue(blend, out var pipeline))
        {
            pipeline = BuildVariant(blend);
            variants[blend] = pipeline;
        }

        var recorder = backend.Recorder;

        var draw = new RecordedDraw
        {
            Pipeline = pipeline,
            VertexCount = (uint)vertexCount,
            InstanceCount = (uint)instanceCount,
            FirstVertex = (uint)firstVertex,
            FirstInstance = 0
        };

        if (hasTextureGroup)
        {
            if (resources is not WebGpuResourceSet wgpuResources)
                throw new InvalidOperationException("Pipeline expects a resource set with textures");

            var (group, groupIndex) = wgpuResources.ResolveBindGroup();
            draw.HasTexture = true;
            draw.TextureGroup = group;
            draw.TextureGroupIndex = groupIndex;
        }

        switch (UniformState.ActiveStrategy)
        {
            case WebGpuUniformState.Strategy.PushConstants:
                if (UniformState.HasValueWritten)
                {
                    draw.UniformKind = UniformKind.PushConstants;
                    draw.PushStart = recorder.AddPushConstants(UniformState.CurrentValueBytes);
                    draw.PushLen = (int)UniformState.PayloadSize;
                }

                break;

            case WebGpuUniformState.Strategy.DynamicOffsetBuffer:
                if (UniformState.HasValueWritten)
                {
                    var (ugroup, uoffset) = UniformState.StageDynamic(backend, bindCache);
                    draw.UniformKind = UniformKind.DynamicOffset;
                    draw.UniformGroup = ugroup;
                    draw.UniformGroupIndex = bindCache.UniformGroupIndex;
                    draw.UniformOffset = uoffset;
                }

                break;
        }

        draw.VbStart = recorder.BeginVertexBindings();
        var vbCount = 0;
        foreach (var binding in vertexBuffers)
        {
            if (binding.Buffer is not WebGpuGraphicsBuffer wgpuBuf) continue;
            if (wgpuBuf.Buffer.IsNull) continue;

            var offset = binding.Offset >= 0 ? (ulong)binding.Offset : (ulong)wgpuBuf.CurrentReadOffset;
            recorder.AddVertexBinding((uint)binding.Slot, wgpuBuf.Buffer, offset);
            ++vbCount;
        }

        draw.VbCount = vbCount;

        if (backend.TryGetViewport(out var vx, out var vy, out var vw, out var vh))
        {
            draw.HasViewport = true;
            draw.VpX = vx;
            draw.VpY = vy;
            draw.VpW = vw;
            draw.VpH = vh;
        }

        if (backend.ScissorEnabled && backend.TryGetScissor(out var sx, out var sy, out var sw, out var sh))
        {
            draw.ScissorEnabled = true;
            draw.ScX = sx;
            draw.ScY = sy;
            draw.ScW = sw;
            draw.ScH = sh;
        }

        recorder.AddDraw(in draw);
    }

    RenderPipeline BuildVariant(BlendingFactorState blend)
    {
        var colorTarget = new ColorTargetState(backend.SurfaceFormat)
        {
            Blend = WgpuMapper.ToWgpu(blend),
            WriteMask = ColorWriteMask.All
        };

        var primitive = PrimitiveState.Default;
        primitive.Topology = WgpuMapper.ToWgpu(Description.Topology);
        primitive.CullMode = WGPUCullMode.None;
        primitive.FrontFace = WGPUFrontFace.CCW;

        return deviceContext.Device.CreateRenderPipeline(
            vertexModule,
            vertexEntryUtf8,
            fragmentModule,
            fragmentEntryUtf8,
            [colorTarget],
            vertexPlan.Buffers,
            vertexPlan.Attributes,
            wgpuPipelineLayout,
            primitive);
    }
}

readonly struct WgpuVertexLayoutPlan
{
    public WgpuVertexLayoutPlan(WgpuVertexBufferLayout[] buffers, WgpuVertexAttribute[] attributes)
    {
        Buffers = buffers;
        Attributes = attributes;
    }

    public WgpuVertexBufferLayout[] Buffers { get; }
    public WgpuVertexAttribute[] Attributes { get; }
}