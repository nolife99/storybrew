namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using Renderers;
using Shaders;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using WgpuBuffer = Ahjo.Wgpu.Buffer;
using VertexAttribute = Ahjo.Wgpu.VertexAttribute;

public sealed class WebGpuRenderPipeline : IRenderPipeline
{
    const int UniformBufferSize = 1024 * 1024;
    const int UniformBindingSize = 256;
    const int UniformAlignment = 256;

    readonly WebGpuGraphicsBackend backend;
    readonly WebGpuGraphicsDevice device;
    readonly Dictionary<BlendingFactorState, RenderPipeline> renderPipelines = [];
    readonly VertexBufferBinding[] vertexBuffers;
    BlendingFactorState boundBlendState;
    RenderPipeline boundPipeline;

    uint boundRenderPassSerial = uint.MaxValue;
    uint boundRenderStateSerial = uint.MaxValue;
    uint boundUniformOffset = uint.MaxValue;
    uint currentUniformOffset;
    bool hasUniformValue, registeredForUniformFlush, disposed;
    PipelineLayout pipelineLayout;
    BindGroup uniformBindGroup;
    BindGroupLayout uniformBindGroupLayout;
    WgpuBuffer uniformBuffer;
    int uniformDirtyEnd;
    int uniformDirtyStart = int.MaxValue;
    uint uniformFrameSerial;
    int uniformOffset, currentUniformSize;
    byte[] uniformShadow;

    ShaderModule vertexShader, fragmentShader;

    public WebGpuRenderPipeline(WebGpuGraphicsBackend backend, RenderPipelineDescription description)
    {
        if (description.ShaderSource.Language != ShaderSourceLanguage.Wgsl)
            throw new NotSupportedException("WebGPU render pipelines require WGSL sources");

        this.backend = backend;
        device = (WebGpuGraphicsDevice)backend.Device;
        Description = description;
        vertexBuffers = new VertexBufferBinding[description.VertexInput.Buffers.Length];

        for (var i = 0; i < vertexBuffers.Length; ++i)
            vertexBuffers[i].Layout = description.VertexInput.Buffers[i];

        try
        {
            vertexShader = createShaderModule(description.ShaderSource.VertexSource);
            fragmentShader = createShaderModule(description.ShaderSource.FragmentSource);
            uniformBindGroupLayout = createUniformBindGroupLayout();
            TextureBindGroupLayout = createTextureBindGroupLayout(description.PipelineLayout);
            pipelineLayout = createPipelineLayout();
            createUniformBuffer();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    static ReadOnlySpan<byte> MainEntryPoint => "main"u8;

    internal BindGroupLayout TextureBindGroupLayout { get; private set; }

    public GraphicsResourceHandle NativeHandle => new(backend.Name, boundPipeline.NativeHandle());
    public RenderPipelineDescription Description { get; }

    public IRenderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name)
        => new WebGpuRenderUniform<T>(this, name.ToString());

    public IRenderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform)
        => new WebGpuRenderUniform<T>(this, uniform.Name);

    public IResourceSet CreateResourceSet()
        => new WebGpuResourceSet(backend, this, Description.PipelineLayout);

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (backend.HasReadyRenderPass)
            EnsureBound();
    }

    public void Unbind() { }

    public void BindVertexBuffer(int slot, IGraphicsBuffer buffer)
        => BindVertexBuffer(slot, buffer, buffer is WebGpuGraphicsBuffer webGpuBuffer ? webGpuBuffer.BindingOffset : 0);

    public void BindVertexBuffer(int slot, IGraphicsBuffer buffer, int offset)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), offset, null);

        if (buffer is not WebGpuGraphicsBuffer webGpuBuffer)
            throw new InvalidOperationException($"{nameof(WebGpuRenderPipeline)} can only bind WebGPU buffers");

        for (var i = 0; i < vertexBuffers.Length; ++i)
            if (vertexBuffers[i].Layout.Slot == slot)
            {
                vertexBuffers[i].Buffer = webGpuBuffer;
                vertexBuffers[i].Offset = offset;
                vertexBuffers[i].BoundSerial = uint.MaxValue;
                return;
            }

        throw new ArgumentException($"Vertex buffer slot {slot} is not part of this render pipeline", nameof(slot));
    }

    public void Draw(DrawCommand command)
    {
        if (command.VertexCount == 0) return;
        if (!backend.TryRequireRenderPass()) return;

        EnsureBound();
        backend.Draw((uint)command.VertexCount, 1, (uint)command.FirstVertex);
    }

    public void DrawInstanced(DrawInstancedCommand command)
    {
        if (command.VertexCount == 0 || command.InstanceCount == 0) return;
        if (!backend.TryRequireRenderPass()) return;

        EnsureBound();
        backend.Draw((uint)command.VertexCount, (uint)command.InstanceCount, (uint)command.FirstVertex);
    }

    public void Dispose()
    {
        if (disposed) return;

        foreach (var pipeline in renderPipelines.Values)
            backend.DeferredReleases.Retire(pipeline);

        if (!uniformBindGroup.IsNull) backend.DeferredReleases.Retire(uniformBindGroup);
        if (!uniformBuffer.IsNull) backend.DeferredReleases.Retire(uniformBuffer);

        uniformShadow = null;

        if (!pipelineLayout.IsNull) pipelineLayout.Dispose();
        if (!TextureBindGroupLayout.IsNull) TextureBindGroupLayout.Dispose();
        if (!uniformBindGroupLayout.IsNull) uniformBindGroupLayout.Dispose();
        if (!fragmentShader.IsNull) fragmentShader.Dispose();
        if (!vertexShader.IsNull) vertexShader.Dispose();

        renderPipelines.Clear();
        uniformBindGroup = default;
        uniformBuffer = default;
        pipelineLayout = default;
        TextureBindGroupLayout = default;
        uniformBindGroupLayout = default;
        fragmentShader = default;
        vertexShader = default;
        disposed = true;
    }

    internal void EnsureBound()
    {
        var currentSerial = backend.RenderPassSerial;
        var newPass = boundRenderPassSerial != currentSerial;
        var blendState = device.BlendState;
        var pipeline = getRenderPipeline(blendState);

        if (newPass || boundPipeline.NativeHandle() != pipeline.NativeHandle() || boundBlendState != blendState)
        {
            backend.SetRenderPipeline(pipeline);
            boundPipeline = pipeline;
            boundBlendState = blendState;
        }

        var renderStateSerial = device.RenderPassStateSerial;
        if (newPass || boundRenderStateSerial != renderStateSerial)
        {
            device.ApplyRenderPassState();
            boundRenderPassSerial = currentSerial;
            boundRenderStateSerial = renderStateSerial;
            boundUniformOffset = uint.MaxValue;
        }

        if (hasUniformValue)
        {
            var dynamicOffset = currentUniformOffset;
            if (newPass || boundUniformOffset != dynamicOffset)
            {
                backend.SetBindGroup(0, uniformBindGroup, dynamicOffset);
                boundUniformOffset = dynamicOffset;
            }
        }

        var layouts = Description.VertexInput.Buffers;
        for (var i = 0; i < vertexBuffers.Length; ++i)
        {
            ref var binding = ref vertexBuffers[i];
            var webGpuBuffer = binding.Buffer;
            if (webGpuBuffer is null)
                throw new InvalidOperationException($"Vertex buffer slot {layouts[i].Slot} is not bound");

            var handle = webGpuBuffer.BufferHandle;
            if (handle.IsNull) continue;
            if (binding.BoundSerial == currentSerial &&
                binding.BoundHandle.NativeHandle() == handle.NativeHandle() &&
                binding.BoundOffset == binding.Offset)
                continue;

            backend.SetVertexBuffer((uint)layouts[i].Slot,
                handle,
                (ulong)binding.Offset,
                ulong.MaxValue);

            binding.BoundSerial = currentSerial;
            binding.BoundHandle = handle;
            binding.BoundOffset = binding.Offset;
        }
    }

    internal void SetUniform<T>(string name, T value)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new NotSupportedException($"WebGPU uniform '{name}' must be an unmanaged value");

        var size = Unsafe.SizeOf<T>();
        if (size > UniformBindingSize)
            throw new NotSupportedException($"WebGPU uniform '{name}' is {size} bytes; max supported size is {UniformBindingSize}");

        var frameSerial = backend.FrameSerial;
        if (uniformFrameSerial != frameSerial)
        {
            uniformFrameSerial = frameSerial;
            uniformOffset = 0;
            uniformDirtyStart = int.MaxValue;
            uniformDirtyEnd = 0;
        }

        var valueBytes = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref value), size);
        if (hasUniformValue &&
            currentUniformSize == size &&
            uniformShadow.AsSpan((int)currentUniformOffset, size).SequenceEqual(valueBytes))
            return;

        var offset = align(uniformOffset, UniformAlignment);
        var nextOffset = checked(offset + UniformBindingSize);
        if (nextOffset > UniformBufferSize)
            throw new InvalidOperationException("WebGPU uniform ring exhausted for this frame");

        uniformOffset = nextOffset;
        currentUniformOffset = (uint)offset;
        currentUniformSize = size;
        hasUniformValue = true;

        valueBytes.CopyTo(uniformShadow.AsSpan(offset, size));

        if (offset < uniformDirtyStart) uniformDirtyStart = offset;
        var end = offset + size;
        if (end > uniformDirtyEnd) uniformDirtyEnd = end;

        if (registeredForUniformFlush) return;

        backend.RegisterPipelineForUniformFlush(this);
        registeredForUniformFlush = true;
    }

    internal void FlushUniforms()
    {
        registeredForUniformFlush = false;
        if (uniformDirtyStart >= uniformDirtyEnd) return;

        var length = uniformDirtyEnd - uniformDirtyStart;
        backend.QueueHandle.WriteBuffer(uniformBuffer,
            (ulong)uniformDirtyStart,
            uniformShadow.AsSpan(uniformDirtyStart, length));

        uniformDirtyStart = int.MaxValue;
        uniformDirtyEnd = 0;
    }

    RenderPipeline getRenderPipeline(BlendingFactorState blendState)
    {
        if (renderPipelines.TryGetValue(blendState, out var pipeline)) return pipeline;

        var renderPipeline = createRenderPipeline(blendState);
        renderPipelines.Add(blendState, renderPipeline);
        return renderPipeline;
    }

    RenderPipeline createRenderPipeline(BlendingFactorState blendState)
    {
        var attributeCount = 0;
        foreach (var buffer in Description.VertexInput.Buffers)
        foreach (var element in buffer.Elements)
            attributeCount += element.Format.GetLocationCount();

        Span<VertexBufferLayout> bufferLayouts = stackalloc VertexBufferLayout[Description.VertexInput.Buffers.Length];
        Span<VertexAttribute> attributes = stackalloc VertexAttribute[attributeCount];
        var attributeIndex = 0;

        for (var i = 0; i < Description.VertexInput.Buffers.Length; ++i)
        {
            var layout = Description.VertexInput.Buffers[i];
            var firstAttribute = attributeIndex;

            foreach (var element in layout.Elements)
            {
                var locationCount = element.Format.GetLocationCount();
                for (var column = 0; column < locationCount; ++column)
                {
                    attributes[attributeIndex] = new(toVertexFormat(element.Format),
                        (ulong)(element.Offset + element.Format.GetLocationOffset(column)),
                        (uint)attributeIndex);

                    ++attributeIndex;
                }
            }

            bufferLayouts[i] = new((ulong)layout.Stride,
                attributeIndex - firstAttribute,
                toStepMode(layout.InputRate));
        }

        return createRenderPipeline(blendState, bufferLayouts, attributes);
    }

    RenderPipeline createRenderPipeline(BlendingFactorState blendState,
        scoped ReadOnlySpan<VertexBufferLayout> bufferLayouts,
        scoped ReadOnlySpan<VertexAttribute> attributes)
    {
        var blend = new WGPUBlendState
        {
            color = new()
            {
                operation = WGPUBlendOperation.Add,
                srcFactor = toBlendFactor(blendState.Source),
                dstFactor = toBlendFactor(blendState.Destination)
            },
            alpha = new()
            {
                operation = WGPUBlendOperation.Add,
                srcFactor = toBlendFactor(blendState.AlphaSource),
                dstFactor = toBlendFactor(blendState.AlphaDestination)
            }
        };

        Span<ColorTargetState> targets = stackalloc ColorTargetState[1];
        targets[0] = new(backend.SurfaceFormat)
        {
            Blend = blendState.Enabled ? blend : null,
            WriteMask = ColorWriteMask.All
        };

        var primitive = new PrimitiveState
        {
            Topology = toPrimitiveTopology(Description.Topology),
            FrontFace = WGPUFrontFace.CCW,
            CullMode = WGPUCullMode.None
        };

        return backend.DeviceHandle.CreateRenderPipeline(vertexShader,
            MainEntryPoint,
            fragmentShader,
            MainEntryPoint,
            targets,
            bufferLayouts,
            attributes,
            pipelineLayout,
            primitive,
            MultisampleState.Default);
    }

    ShaderModule createShaderModule(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var descriptor = new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(bytes)
        };

        return backend.DeviceHandle.CreateShaderModule(in descriptor);
    }

    BindGroupLayout createUniformBindGroupLayout() 
        => backend.DeviceHandle.CreateBindGroupLayout([BindGroupLayoutEntry.Buffer(0,
        ShaderStage.Vertex,
        WGPUBufferBindingType.Uniform,
        true)]);

    BindGroupLayout createTextureBindGroupLayout(Backend.PipelineLayout layout)
    {
        var textureBindings = layout.TextureBindings;
        if (textureBindings.Length == 0) return default;

        if (textureBindings.Length > 1)
            throw new NotSupportedException("Core WebGPU renderer currently supports one texture binding layout");

        return createCoreTextureBindGroupLayout(textureBindings[0].Capacity);
    }

    BindGroupLayout createCoreTextureBindGroupLayout(int capacity)
    {
        var entryCount = checked(capacity * 2);
        if (backend.MaxBindingsPerBindGroup != 0 && entryCount > backend.MaxBindingsPerBindGroup)
            throw new InvalidOperationException($"Texture binding layout requires {entryCount} bindings, but device limit is {backend.MaxBindingsPerBindGroup}");

        using var entries = TempList.Create<BindGroupLayoutEntry>(entryCount);
        for (var i = 0; i < capacity; ++i)
            entries.AddRange([
                BindGroupLayoutEntry.Texture((uint)(i * 2),
                    ShaderStage.Fragment),
                BindGroupLayoutEntry.Sampler((uint)(i * 2 + 1),
                    ShaderStage.Fragment)
            ]);

        return backend.DeviceHandle.CreateBindGroupLayout(entries.AsReadOnlySpan());
    }

    PipelineLayout createPipelineLayout()
    {
        var layouts = TextureBindGroupLayout.IsNull
            ? stackalloc BindGroupLayout[1]
            : stackalloc BindGroupLayout[2];

        layouts[0] = uniformBindGroupLayout;
        if (!TextureBindGroupLayout.IsNull)
            layouts[1] = TextureBindGroupLayout;

        return backend.DeviceHandle.CreatePipelineLayout(layouts);
    }

    void createUniformBuffer()
    {
        BufferDescriptor bufferDescriptor = new()
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = UniformBufferSize
        };

        uniformBuffer = backend.DeviceHandle.CreateBuffer(in bufferDescriptor);
        uniformShadow = GC.AllocateUninitializedArray<byte>(UniformBufferSize);
        uniformBindGroup = backend.DeviceHandle.CreateBindGroup(uniformBindGroupLayout, [BindGroupEntry.Buffer(0, uniformBuffer, 0, UniformBindingSize)]);
    }

    static int align(int value, int alignment)
        => value + alignment - 1 & ~(alignment - 1);

    static WGPUPrimitiveTopology toPrimitiveTopology(PrimitiveTopology topology)
        => topology switch
        {
            PrimitiveTopology.Lines => WGPUPrimitiveTopology.LineList,
            PrimitiveTopology.Triangles => WGPUPrimitiveTopology.TriangleList,
            _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
        };

    static WGPUVertexStepMode toStepMode(VertexInputRate inputRate)
        => inputRate switch
        {
            VertexInputRate.Vertex => WGPUVertexStepMode.Vertex,
            VertexInputRate.Instance => WGPUVertexStepMode.Instance,
            _ => throw new ArgumentOutOfRangeException(nameof(inputRate), inputRate, null)
        };

    static WGPUVertexFormat toVertexFormat(VertexAttributeFormat format)
        => format switch
        {
            VertexAttributeFormat.Float32 => WGPUVertexFormat.Float32,
            VertexAttributeFormat.Float32x2 => WGPUVertexFormat.Float32x2,
            VertexAttributeFormat.Float32Mat3x2 => WGPUVertexFormat.Float32x2,
            VertexAttributeFormat.Float32x3 => WGPUVertexFormat.Float32x3,
            VertexAttributeFormat.Float32x4 => WGPUVertexFormat.Float32x4,
            VertexAttributeFormat.Float16x2 => WGPUVertexFormat.Float16x2,
            VertexAttributeFormat.Float16x4 => WGPUVertexFormat.Float16x4,
            VertexAttributeFormat.Unorm8x4 => WGPUVertexFormat.Unorm8x4,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    static WGPUBlendFactor toBlendFactor(BlendFactor factor)
        => factor switch
        {
            BlendFactor.Zero => WGPUBlendFactor.Zero,
            BlendFactor.One => WGPUBlendFactor.One,
            BlendFactor.SrcAlpha => WGPUBlendFactor.SrcAlpha,
            BlendFactor.OneMinusSrcAlpha => WGPUBlendFactor.OneMinusSrcAlpha,
            _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null)
        };

    readonly struct WebGpuRenderUniform<T>(WebGpuRenderPipeline pipeline, string name) : IRenderUniform<T>
    {
        public void SetValue(T value) => pipeline.SetUniform(name, value);
    }

    struct VertexBufferBinding
    {
        public Backend.VertexBufferLayout Layout;
        public WebGpuGraphicsBuffer Buffer;
        public int Offset;
        public uint BoundSerial;
        public WgpuBuffer BoundHandle;
        public int BoundOffset;
    }
}