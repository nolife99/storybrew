namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Ahjo.Wgpu;
using Ahjo.Wgpu.Native;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using WgpuBuffer = Ahjo.Wgpu.Buffer;
using WgpuPipelineLayout = Ahjo.Wgpu.PipelineLayout;
using WgpuRenderPipeline = Ahjo.Wgpu.RenderPipeline;
using WgpuVertexAttribute = Ahjo.Wgpu.VertexAttribute;
using WgpuVertexBufferLayout = Ahjo.Wgpu.VertexBufferLayout;

public sealed class WebGpuRenderPipeline : IRenderPipeline
{
    readonly WebGpuGraphicsBackend backend;
    readonly WebGpuGraphicsDevice device;
    readonly Dictionary<BlendingFactorState, WgpuRenderPipeline> pipelines = [];
    readonly Dictionary<string, UniformValue> uniforms = new(StringComparer.Ordinal);
    bool disposed;
    BindGroupLayout textureBindGroupLayout;
    WebGpuTextureBindingInfo[] textureBindingInfos = [];
    BindGroupLayout uniformBindGroupLayout;
    WgpuPipelineLayout pipelineLayout;
    ShaderModule vertexShader, fragmentShader;

    public WebGpuRenderPipeline(WebGpuGraphicsBackend backend, RenderPipelineDescription description)
    {
        if (description.ShaderSource.Language != ShaderSourceLanguage.Wgsl)
            throw new NotSupportedException("WebGPU render pipelines require WGSL sources");

        this.backend = backend;
        device = (WebGpuGraphicsDevice)backend.Device;
        Description = description;

        try
        {
            vertexShader = createShaderModule(description.ShaderSource.VertexSource);
            fragmentShader = createShaderModule(description.ShaderSource.FragmentSource);
            uniformBindGroupLayout = createUniformBindGroupLayout();
            textureBindGroupLayout = createTextureBindGroupLayout(description.PipelineLayout);
            pipelineLayout = createPipelineLayout();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal BindGroupLayout TextureBindGroupLayout => textureBindGroupLayout;
    public RenderPipelineDescription Description { get; }

    public IRenderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name)
        => new WebGpuRenderUniform<T>(this, name.ToString());

    public IRenderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform)
        => new WebGpuRenderUniform<T>(this, uniform.Name);

    public IResourceSet CreateResourceSet()
        => new WebGpuResourceSet(backend, this, Description.PipelineLayout);

    public void Draw(DrawCommand command, scoped ReadOnlySpan<RenderVertexBufferBinding> vertexBuffers, IResourceSet resources = null)
    {
        validateDrawCommand(command);
        if (command.VertexCount == 0) return;
        draw(command.VertexCount, 1, command.FirstVertex, vertexBuffers, resources);
    }

    public void DrawInstanced(DrawInstancedCommand command, scoped ReadOnlySpan<RenderVertexBufferBinding> vertexBuffers, IResourceSet resources = null)
    {
        validateDrawInstancedCommand(command);
        if (command.VertexCount == 0 || command.InstanceCount == 0) return;
        draw(command.VertexCount, command.InstanceCount, command.FirstVertex, vertexBuffers, resources);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        foreach (var pipeline in pipelines.Values)
            backend.DeferredReleases.Retire(pipeline);
        pipelines.Clear();

        backend.DeferredReleases.Retire(pipelineLayout);
        backend.DeferredReleases.Retire(textureBindGroupLayout);
        backend.DeferredReleases.Retire(uniformBindGroupLayout);
        backend.DeferredReleases.Retire(vertexShader);
        backend.DeferredReleases.Retire(fragmentShader);

        pipelineLayout = default;
        textureBindGroupLayout = default;
        uniformBindGroupLayout = default;
        vertexShader = default;
        fragmentShader = default;
        uniforms.Clear();
    }

    internal WebGpuTextureBindingInfo GetTextureBindingInfo(ShaderSamplerBinding slot)
    {
        foreach (var info in textureBindingInfos)
            if (info.Name == slot.Name)
                return info;
        throw new ArgumentException($"Texture binding '{slot.Name}' is not part of pipeline '{Description.Name}'", nameof(slot));
    }

    internal void SetUniform<T>(string name, T value)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new NotSupportedException($"WebGPU uniform '{name}' must be an unmanaged value");

        var size = Unsafe.SizeOf<T>();
        if (size > backend.UniformBindingSize)
            throw new NotSupportedException($"WebGPU uniform '{name}' is {size} bytes; max supported size is {backend.UniformBindingSize}");

        if (!uniforms.TryGetValue(name, out var uniform) || uniform.Bytes.Length != size)
        {
            uniform = new(size);
            uniforms[name] = uniform;
        }

        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref value), size).CopyTo(uniform.Bytes);
    }

    void draw(int vertexCount,
        int instanceCount,
        int firstVertex,
        scoped ReadOnlySpan<RenderVertexBufferBinding> vertexBuffers,
        IResourceSet resources)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        backend.RequireFrame();
        device.RecordState();
        backend.RecordPipeline(getPipeline(device.BlendState));

        bindUniforms();
        bindResources(resources);
        bindVertexBuffers(vertexBuffers, vertexCount, instanceCount, firstVertex);
        backend.RecordDraw((uint)vertexCount, (uint)instanceCount, (uint)firstVertex, 0);

        backend.RetainForFrame(this);
    }

    void bindUniforms()
    {
        // Still correctness-first: one small uniform buffer per draw. The CPU shadow block is
        // stack/local-pooled now, so this path no longer allocates a byte[] per draw.
        var size = backend.UniformBindingSize;
        byte[] rented = null;
        Span<byte> shadow = size <= 1024
            ? stackalloc byte[size]
            : (rented = ArrayPool<byte>.Shared.Rent(size)).AsSpan(0, size);

        try
        {
            shadow.Clear();

            if (uniforms.TryGetValue("u_combinedMatrix", out var matrix))
                matrix.Bytes.CopyTo(shadow[..matrix.Bytes.Length]);
            else if (uniforms.Count != 0)
            {
                var offset = 0;
                foreach (var value in uniforms.Values)
                {
                    if (offset + value.Bytes.Length > shadow.Length) break;
                    value.Bytes.CopyTo(shadow.Slice(offset, value.Bytes.Length));
                    offset = WebGpuResourceValidation.Align(offset + value.Bytes.Length, 16);
                }
            }

            BufferDescriptor descriptor = new()
            {
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
                Size = (ulong)size
            };

            var uniformBuffer = backend.DeviceHandle.CreateBuffer(in descriptor);
            Span<BindGroupEntry> entries = stackalloc BindGroupEntry[1];
            entries[0] = BindGroupEntry.Buffer(0, uniformBuffer, 0, (ulong)size);
            var bindGroup = backend.DeviceHandle.CreateBindGroup(uniformBindGroupLayout, entries);

            backend.QueueWriteBuffer(uniformBuffer, 0, shadow);
            backend.RecordBindGroup(0, bindGroup);

            backend.RetireAfterSubmit(bindGroup);
            backend.RetireAfterSubmit(uniformBuffer);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    void bindResources(IResourceSet resources)
    {
        if (resources is null)
        {
            if (!textureBindGroupLayout.IsNull)
                throw new InvalidOperationException($"Pipeline '{Description.Name}' requires a resource set");
            return;
        }

        if (textureBindGroupLayout.IsNull)
            throw new InvalidOperationException($"Pipeline '{Description.Name}' has no texture bindings but a resource set was supplied");

        if (resources is not WebGpuResourceSet webGpuResourceSet)
            throw new InvalidOperationException($"{nameof(WebGpuRenderPipeline)} can only use WebGPU resource sets");
        if (!ReferenceEquals(webGpuResourceSet.OwnerBackend, backend))
            throw new InvalidOperationException("Resource set belongs to another backend");
        ObjectDisposedException.ThrowIf(webGpuResourceSet.IsDisposed, webGpuResourceSet);
        if (!ReferenceEquals(webGpuResourceSet.Pipeline, this))
            throw new InvalidOperationException("Resource set was created for a different render pipeline");

        var bindGroup = webGpuResourceSet.GetBindGroup();
        backend.RecordBindGroup(1, bindGroup);
        webGpuResourceSet.RetainForFrame();
    }

    void bindVertexBuffers(scoped ReadOnlySpan<RenderVertexBufferBinding> suppliedBindings,
        int vertexCount,
        int instanceCount,
        int firstVertex)
    {
        var layouts = Description.VertexInput.Buffers;

        for (var i = 0; i < suppliedBindings.Length; ++i)
        {
            var supplied = suppliedBindings[i];
            if (supplied.Slot < 0)
                throw new ArgumentOutOfRangeException(nameof(suppliedBindings), supplied.Slot, "Vertex buffer slot is negative");

            var matched = false;
            foreach (var layout in layouts)
                if (layout.Slot == supplied.Slot)
                    matched = true;

            if (!matched)
                throw new InvalidOperationException($"Vertex buffer slot {supplied.Slot} is not part of pipeline '{Description.Name}'");

            for (var j = i + 1; j < suppliedBindings.Length; ++j)
                if (suppliedBindings[j].Slot == supplied.Slot)
                    throw new InvalidOperationException($"Vertex buffer slot {supplied.Slot} is supplied more than once for pipeline '{Description.Name}'");
        }

        foreach (var layout in layouts)
        {
            var suppliedIndex = -1;
            for (var i = 0; i < suppliedBindings.Length; ++i)
            {
                if (suppliedBindings[i].Slot != layout.Slot) continue;
                suppliedIndex = i;
                break;
            }

            if (suppliedIndex < 0)
                throw new InvalidOperationException($"Vertex buffer slot {layout.Slot} is not supplied for pipeline '{Description.Name}'");

            var supplied = suppliedBindings[suppliedIndex];
            var buffer = WebGpuResourceValidation.RequireBuffer(backend, supplied.Buffer, $"Vertex buffer slot {layout.Slot}");
            var offset = supplied.Offset >= 0 ? supplied.Offset : buffer.BindingOffset;
            var elementCount = layout.InputRate switch
            {
                VertexInputRate.Vertex => checked(firstVertex + vertexCount),
                VertexInputRate.Instance => instanceCount,
                _ => throw new ArgumentOutOfRangeException(nameof(layout.InputRate), layout.InputRate, null)
            };

            var requiredBytes = checked((long)elementCount * layout.Stride);
            WebGpuResourceValidation.ValidateBufferRange(buffer, offset, requiredBytes, $"Vertex buffer slot {layout.Slot}");

            backend.RecordVertexBuffer((uint)layout.Slot, buffer.BufferHandle, (ulong)offset, (ulong)(buffer.SizeInBytes - offset));
            backend.RetainForFrame(buffer);
        }
    }

    WgpuRenderPipeline getPipeline(BlendingFactorState blendState)
    {
        if (pipelines.TryGetValue(blendState, out var pipeline)) return pipeline;
        pipeline = createRenderPipeline(blendState);
        pipelines.Add(blendState, pipeline);
        return pipeline;
    }

    WgpuRenderPipeline createRenderPipeline(BlendingFactorState blendState)
    {
        var attributeCount = 0;
        foreach (var buffer in Description.VertexInput.Buffers)
        foreach (var element in buffer.Elements)
            attributeCount += element.Format.GetLocationCount();

        Span<WgpuVertexBufferLayout> bufferLayouts = stackalloc WgpuVertexBufferLayout[Description.VertexInput.Buffers.Length];
        Span<WgpuVertexAttribute> attributes = stackalloc WgpuVertexAttribute[attributeCount];
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

    WgpuRenderPipeline createRenderPipeline(BlendingFactorState blendState,
        scoped ReadOnlySpan<WgpuVertexBufferLayout> bufferLayouts,
        scoped ReadOnlySpan<WgpuVertexAttribute> attributes)
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

        var vertexEntryPoint = Encoding.UTF8.GetBytes(Description.ShaderSource.VertexEntryPoint);
        var fragmentEntryPoint = Encoding.UTF8.GetBytes(Description.ShaderSource.FragmentEntryPoint);

        return backend.DeviceHandle.CreateRenderPipeline(vertexShader,
            vertexEntryPoint,
            fragmentShader,
            fragmentEntryPoint,
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
        var descriptor = new ShaderModuleDescriptor { Source = ShaderSource.FromWgsl(bytes) };
        return backend.DeviceHandle.CreateShaderModule(in descriptor);
    }

    BindGroupLayout createUniformBindGroupLayout()
        => backend.DeviceHandle.CreateBindGroupLayout([
            BindGroupLayoutEntry.Buffer(0, ShaderStage.Vertex, WGPUBufferBindingType.Uniform, false)
        ]);

    BindGroupLayout createTextureBindGroupLayout(Backend.PipelineLayout layout)
    {
        var textureBindings = layout.TextureBindings;
        textureBindingInfos = new WebGpuTextureBindingInfo[textureBindings.Length];
        if (textureBindings.Length == 0) return default;

        var entryCount = 0;
        foreach (var binding in textureBindings)
        {
            if (string.IsNullOrWhiteSpace(binding.Name))
                throw new ArgumentException("Texture binding layout has an empty logical name", nameof(layout));
            if (binding.Capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(layout), binding.Capacity, $"Texture binding '{binding.Name}' must have positive capacity");
            entryCount = checked(entryCount + binding.Capacity * 2);
        }

        if (backend.MaxBindingsPerBindGroup != 0 && entryCount > backend.MaxBindingsPerBindGroup)
            throw new InvalidOperationException($"Texture binding layout requires {entryCount} bindings, but device limit is {backend.MaxBindingsPerBindGroup}");

        var entries = new BindGroupLayoutEntry[entryCount];
        var nextBinding = 0;
        var entryIndex = 0;

        for (var bindingIndex = 0; bindingIndex < textureBindings.Length; ++bindingIndex)
        {
            var binding = textureBindings[bindingIndex];
            var baseBinding = nextBinding;
            textureBindingInfos[bindingIndex] = new(binding.Name, binding.Capacity, baseBinding);

            for (var i = 0; i < binding.Capacity; ++i)
            {
                entries[entryIndex++] = BindGroupLayoutEntry.Texture((uint)nextBinding++, ShaderStage.Fragment);
                entries[entryIndex++] = BindGroupLayoutEntry.Sampler((uint)nextBinding++, ShaderStage.Fragment);
            }
        }

        return backend.DeviceHandle.CreateBindGroupLayout(entries);
    }

    WgpuPipelineLayout createPipelineLayout()
    {
        BindGroupLayout[] layouts = textureBindGroupLayout.IsNull
            ? [uniformBindGroupLayout]
            : [uniformBindGroupLayout, textureBindGroupLayout];
        return backend.DeviceHandle.CreatePipelineLayout(layouts);
    }

    static void validateDrawCommand(DrawCommand command)
    {
        if (command.VertexCount < 0) throw new ArgumentOutOfRangeException(nameof(command), command.VertexCount, "VertexCount is negative");
        if (command.FirstVertex < 0) throw new ArgumentOutOfRangeException(nameof(command), command.FirstVertex, "FirstVertex is negative");
    }

    static void validateDrawInstancedCommand(DrawInstancedCommand command)
    {
        if (command.VertexCount < 0) throw new ArgumentOutOfRangeException(nameof(command), command.VertexCount, "VertexCount is negative");
        if (command.InstanceCount < 0) throw new ArgumentOutOfRangeException(nameof(command), command.InstanceCount, "InstanceCount is negative");
        if (command.FirstVertex < 0) throw new ArgumentOutOfRangeException(nameof(command), command.FirstVertex, "FirstVertex is negative");
    }

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
            VertexAttributeFormat.Float32x2 or VertexAttributeFormat.Float32Mat3x2 => WGPUVertexFormat.Float32x2,
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

    sealed class UniformValue
    {
        public UniformValue(int size) => Bytes = GC.AllocateUninitializedArray<byte>(size);
        public byte[] Bytes { get; }
    }

    readonly struct WebGpuRenderUniform<T>(WebGpuRenderPipeline pipeline, string name) : IRenderUniform<T>
    {
        public void SetValue(T value) => pipeline.SetUniform(name, value);
    }
}

readonly record struct WebGpuTextureBindingInfo(string Name, int Capacity, int BaseBinding);

public sealed class WebGpuResourceSet : IResourceSet
{
    readonly WebGpuGraphicsBackend backend;
    readonly ResourceBinding[] bindings;
    readonly WebGpuRenderPipeline pipeline;
    BindGroup cachedBindGroup;
    bool disposed;

    public WebGpuResourceSet(WebGpuGraphicsBackend backend, WebGpuRenderPipeline pipeline, Backend.PipelineLayout layout)
    {
        this.backend = backend;
        this.pipeline = pipeline;

        var pipelineTextureBindings = layout.TextureBindings;
        bindings = new ResourceBinding[pipelineTextureBindings.Length];
        for (var i = 0; i < pipelineTextureBindings.Length; ++i)
        {
            var layoutBinding = pipelineTextureBindings[i];
            var backendBinding = pipeline.GetTextureBindingInfo(layoutBinding.Slot);
            bindings[i] = new(layoutBinding.Slot, backendBinding.BaseBinding, backendBinding.Capacity);
        }
    }

    internal bool IsDisposed => disposed;
    internal WebGpuGraphicsBackend OwnerBackend => backend;
    internal WebGpuRenderPipeline Pipeline => pipeline;

    public void SetTextures(ShaderSamplerBinding slot, scoped ReadOnlySpan<ITexture> textures)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        ref var binding = ref getBinding(slot);
        if (textures.Length > binding.Capacity)
            throw new ArgumentException($"Texture binding '{slot.Name}' accepts at most {binding.Capacity} textures", nameof(textures));

        var changed = binding.TextureCount != textures.Length;
        if (textures.Length == 0)
        {
            if (binding.TextureCount != 0)
                binding.Clear();
            if (changed)
                InvalidateBindGroup();
            return;
        }

        for (var i = 0; i < textures.Length; ++i)
        {
            var texture = WebGpuResourceValidation.RequireTexture(backend, textures[i], $"Texture binding '{slot.Name}'[{i}]");
            if (!ReferenceEquals(binding.Textures[i], texture))
            {
                binding.Textures[i] = texture;
                changed = true;
            }
        }

        if (binding.TextureCount > textures.Length)
            Array.Clear(binding.Textures, textures.Length, binding.TextureCount - textures.Length);
        binding.TextureCount = textures.Length;

        if (changed)
            InvalidateBindGroup();
    }

    internal BindGroup GetBindGroup()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (bindings.Length == 0) return default;
        if (!cachedBindGroup.IsNull) return cachedBindGroup;

        var entryCount = 0;
        foreach (var binding in bindings)
        {
            if (binding.TextureCount == 0)
                throw new InvalidOperationException($"Texture binding '{binding.Name}' has no textures assigned");
            entryCount = checked(entryCount + binding.Capacity * 2);
        }

        Span<BindGroupEntry> entries = stackalloc BindGroupEntry[entryCount];
        var entryIndex = 0;
        foreach (var binding in bindings)
        {
            var fallback = WebGpuResourceValidation.RequireTexture(backend, binding.Textures[0], $"Texture binding '{binding.Name}'[0]");
            for (var i = 0; i < binding.Capacity; ++i)
            {
                var texture = i < binding.TextureCount
                    ? WebGpuResourceValidation.RequireTexture(backend, binding.Textures[i], $"Texture binding '{binding.Name}'[{i}]")
                    : fallback;

                entries[entryIndex++] = BindGroupEntry.TextureView((uint)(binding.BaseBinding + i * 2), texture.TextureViewHandle);
                entries[entryIndex++] = BindGroupEntry.Sampler((uint)(binding.BaseBinding + i * 2 + 1), texture.SamplerHandle);
            }
        }

        cachedBindGroup = backend.DeviceHandle.CreateBindGroup(pipeline.TextureBindGroupLayout, entries);
        return cachedBindGroup;
    }

    internal void RetainForFrame()
    {
        backend.RetainForFrame(this);
        foreach (var binding in bindings)
        {
            var fallback = binding.TextureCount != 0 ? binding.Textures[0] : null;
            for (var i = 0; i < binding.Capacity; ++i)
            {
                var texture = i < binding.TextureCount ? binding.Textures[i] : fallback;
                if (texture is not null)
                    backend.RetainForFrame(texture);
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        InvalidateBindGroup();
        foreach (var binding in bindings)
            binding.Dispose();
        disposed = true;
    }

    void InvalidateBindGroup()
    {
        if (cachedBindGroup.IsNull) return;
        backend.RetireAfterSubmit(cachedBindGroup);
        cachedBindGroup = default;
    }

    ref ResourceBinding getBinding(ShaderSamplerBinding slot)
    {
        for (var i = 0; i < bindings.Length; ++i)
            if (bindings[i].Name == slot.Name)
                return ref bindings[i];
        throw new ArgumentException($"Texture binding '{slot.Name}' is not part of this resource set", nameof(slot));
    }

    struct ResourceBinding
    {
        public ResourceBinding(ShaderSamplerBinding slot, int baseBinding, int capacity)
        {
            Slot = slot;
            BaseBinding = baseBinding;
            Capacity = capacity;
            Textures = new WebGpuTexture[capacity];
            TextureCount = 0;
        }

        public readonly ShaderSamplerBinding Slot;
        public readonly string Name => Slot.Name;
        public readonly int BaseBinding;
        public readonly int Capacity;
        public readonly WebGpuTexture[] Textures;
        public int TextureCount;

        public void Clear()
        {
            if (TextureCount != 0)
                Array.Clear(Textures, 0, TextureCount);
            TextureCount = 0;
        }

        public void Dispose()
        {
            if (Textures is not null)
                Array.Clear(Textures);
            TextureCount = 0;
        }
    }
}

public sealed class WebGpuRenderPipelineFactory(WebGpuGraphicsBackend backend) : IRenderPipelineFactory
{
    public IRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
        => new WebGpuRenderPipeline(backend, description);
}
