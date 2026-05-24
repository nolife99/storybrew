namespace BrewLib.Graphics.Backend.WebGPU;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Shaders;
using Silk.NET.WebGPU;
using PrimitiveTopology = Renderers.PrimitiveTopology;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;
using WgpuPrimitiveTopology = Silk.NET.WebGPU.PrimitiveTopology;
using WgpuVertexAttribute = Silk.NET.WebGPU.VertexAttribute;

public unsafe sealed class WebGpuRenderPipeline : IRenderPipeline
{
    const int UniformBufferSize = 1024 * 1024;
    const int UniformBindingSize = 256;
    const int UniformAlignment = 256;

    readonly WebGpuGraphicsBackend backend;
    readonly WebGpuGraphicsDevice device;
    readonly Dictionary<BlendingFactorState, WebGpuHandle<RenderPipeline>> renderPipelines = [];
    readonly VertexBufferBinding[] vertexBuffers;
    BlendingFactorState boundBlendState;
    RenderPipeline* boundPipeline;

    uint boundRenderPassSerial = uint.MaxValue;
    uint boundUniformOffset = uint.MaxValue;
    uint currentUniformOffset;
    bool hasUniformValue, registeredForUniformFlush, disposed;
    PipelineLayout* pipelineLayout;
    BindGroup* uniformBindGroup;
    BindGroupLayout* uniformBindGroupLayout;
    WgpuBuffer* uniformBuffer;
    int uniformDirtyEnd;
    int uniformDirtyStart = int.MaxValue;
    uint uniformFrameSerial;
    int uniformOffset, currentUniformSize;
    byte[] uniformShadow;

    ShaderModule* vertexShader, fragmentShader;

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

    internal BindGroupLayout* TextureBindGroupLayout { get; private set; }

    public GraphicsResourceHandle NativeHandle => new(backend.Name, (nint)boundPipeline);
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

        if (backend.TryGetReadyRenderPass(out var renderPass)) EnsureBound(renderPass);
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

        if (!backend.TryRequireRenderPass(out var renderPass)) return;

        EnsureBound(renderPass);
        backend.Api.RenderPassEncoderDraw(renderPass,
            (uint)command.VertexCount,
            1,
            (uint)command.FirstVertex,
            0);
    }

    public void DrawInstanced(DrawInstancedCommand command)
    {
        if (command.VertexCount == 0 || command.InstanceCount == 0) return;

        if (!backend.TryRequireRenderPass(out var renderPass)) return;

        EnsureBound(renderPass);
        backend.Api.RenderPassEncoderDraw(renderPass,
            (uint)command.VertexCount,
            (uint)command.InstanceCount,
            (uint)command.FirstVertex,
            0);
    }

    public void DrawIndirect(DrawIndirectCommand command)
    {
        if (command.DrawCount == 0) return;

        if (command.Offset < 0)
            throw new ArgumentOutOfRangeException(nameof(command), command.Offset, "Offset must be non-negative.");

        if (command.DrawCount < 0)
            throw new ArgumentOutOfRangeException(nameof(command), command.DrawCount, "Draw count must be non-negative.");

        if (command.Buffer is not WebGpuGraphicsBuffer webGpuBuffer)
            throw new InvalidOperationException($"{nameof(WebGpuRenderPipeline)} can only draw from WebGPU indirect buffers");

        if (webGpuBuffer.BufferHandle is null)
            return;

        if (!backend.TryRequireRenderPass(out var renderPass)) return;

        EnsureBound(renderPass);

        var stride = Unsafe.SizeOf<IndirectDrawCommand>();
        var offset = webGpuBuffer.BindingOffset + command.Offset;
        for (var i = 0; i < command.DrawCount; ++i)
            backend.Api.RenderPassEncoderDrawIndirect(renderPass,
                webGpuBuffer.BufferHandle,
                (ulong)(offset + i * stride));
    }

    public void Dispose()
    {
        if (disposed) return;

        foreach (var pipeline in renderPipelines.Values)
            backend.RetireRenderPipeline(pipeline.Pointer);

        if (uniformBindGroup is not null) backend.RetireBindGroup(uniformBindGroup);
        if (uniformBuffer is not null) backend.RetireBuffer(uniformBuffer);

        uniformShadow = null;

        if (pipelineLayout is not null) backend.Api.PipelineLayoutRelease(pipelineLayout);
        if (TextureBindGroupLayout is not null) backend.Api.BindGroupLayoutRelease(TextureBindGroupLayout);
        if (uniformBindGroupLayout is not null) backend.Api.BindGroupLayoutRelease(uniformBindGroupLayout);
        if (fragmentShader is not null) backend.Api.ShaderModuleRelease(fragmentShader);
        if (vertexShader is not null) backend.Api.ShaderModuleRelease(vertexShader);

        renderPipelines.Clear();
        uniformBindGroup = null;
        uniformBuffer = null;
        pipelineLayout = null;
        TextureBindGroupLayout = null;
        uniformBindGroupLayout = null;
        fragmentShader = null;
        vertexShader = null;
        disposed = true;
    }

    internal void EnsureBound(RenderPassEncoder* renderPass)
    {
        var currentSerial = backend.RenderPassSerial;
        var newPass = boundRenderPassSerial != currentSerial;
        var blendState = device.BlendState;
        var pipeline = getRenderPipeline(blendState);

        if (newPass || boundPipeline != pipeline || boundBlendState != blendState)
        {
            backend.SetRenderPipeline(pipeline);
            boundPipeline = pipeline;
            boundBlendState = blendState;
        }

        if (newPass)
        {
            device.ApplyRenderPassState(renderPass);
            boundRenderPassSerial = currentSerial;
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
            if (handle is null) continue;
            if (binding.BoundSerial == currentSerial &&
                binding.BoundHandle == handle &&
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

        if (!registeredForUniformFlush)
        {
            backend.RegisterPipelineForUniformFlush(this);
            registeredForUniformFlush = true;
        }
    }

    internal void FlushUniforms()
    {
        registeredForUniformFlush = false;
        if (uniformDirtyStart >= uniformDirtyEnd) return;

        var length = uniformDirtyEnd - uniformDirtyStart;
        var source = uniformShadow.AsSpan(uniformDirtyStart, length);
        backend.Api.QueueWriteBuffer(backend.QueueHandle,
            uniformBuffer,
            (ulong)uniformDirtyStart,
            source,
            (nuint)length);

        uniformDirtyStart = int.MaxValue;
        uniformDirtyEnd = 0;
    }

    RenderPipeline* getRenderPipeline(BlendingFactorState blendState)
    {
        if (renderPipelines.TryGetValue(blendState, out var pipeline)) return pipeline.Pointer;

        var renderPipeline = createRenderPipeline(blendState);
        renderPipelines.Add(blendState, new(renderPipeline));
        return renderPipeline;
    }

    RenderPipeline* createRenderPipeline(BlendingFactorState blendState)
    {
        var attributeCount = 0;
        foreach (var buffer in Description.VertexInput.Buffers)
        foreach (var element in buffer.Elements)
            attributeCount += element.Format.GetLocationCount();

        var bufferLayouts = new VertexBufferLayout[Description.VertexInput.Buffers.Length];
        var attributes = new WgpuVertexAttribute[attributeCount];
        var attributeIndex = 0;

        for (var i = 0; i < Description.VertexInput.Buffers.Length; ++i)
        {
            var layout = Description.VertexInput.Buffers[i];
            var firstAttribute = attributeIndex;

            bufferLayouts[i] = new()
            {
                ArrayStride = (ulong)layout.Stride,
                StepMode = toStepMode(layout.InputRate)
            };

            foreach (var element in layout.Elements)
            {
                var locationCount = element.Format.GetLocationCount();
                for (var column = 0; column < locationCount; ++column)
                {
                    attributes[attributeIndex] = new()
                    {
                        Format = toVertexFormat(element.Format),
                        Offset = (ulong)(element.Offset + element.Format.GetLocationOffset(column)),
                        ShaderLocation = (uint)attributeIndex
                    };

                    ++attributeIndex;
                }
            }

            bufferLayouts[i].AttributeCount = (nuint)(attributeIndex - firstAttribute);
        }

        fixed (WgpuVertexAttribute* attributesPointer = attributes)
        {
            attributeIndex = 0;
            for (var i = 0; i < bufferLayouts.Length; ++i)
            {
                bufferLayouts[i].Attributes = attributesPointer + attributeIndex;
                attributeIndex += (int)bufferLayouts[i].AttributeCount;
            }

            return createRenderPipeline(blendState, bufferLayouts);
        }
    }

    RenderPipeline* createRenderPipeline(BlendingFactorState blendState,
        VertexBufferLayout[] bufferLayouts)
    {
        var entryPoint = "main\0"u8.ToArray();

        fixed (byte* entryPointPointer = entryPoint)
        fixed (VertexBufferLayout* bufferLayoutsPointer = bufferLayouts)
        {
            BlendState blend = new()
            {
                Color = new()
                {
                    Operation = BlendOperation.Add,
                    SrcFactor = toBlendFactor(blendState.Source),
                    DstFactor = toBlendFactor(blendState.Destination)
                },
                Alpha = new()
                {
                    Operation = BlendOperation.Add,
                    SrcFactor = toBlendFactor(blendState.AlphaSource),
                    DstFactor = toBlendFactor(blendState.AlphaDestination)
                }
            };

            ColorTargetState colorTarget = new()
            {
                Format = backend.SurfaceFormat,
                Blend = blendState.Enabled ? &blend : null,
                WriteMask = ColorWriteMask.All
            };

            FragmentState fragmentState = new()
            {
                Module = fragmentShader,
                EntryPoint = entryPointPointer,
                TargetCount = 1,
                Targets = &colorTarget
            };

            RenderPipelineDescriptor descriptor = new()
            {
                Layout = pipelineLayout,
                Vertex = new()
                {
                    Module = vertexShader,
                    EntryPoint = entryPointPointer,
                    BufferCount = (nuint)bufferLayouts.Length,
                    Buffers = bufferLayoutsPointer
                },
                Primitive = new()
                {
                    Topology = toPrimitiveTopology(Description.Topology),
                    FrontFace = FrontFace.Ccw,
                    CullMode = CullMode.None
                },
                Multisample = new()
                {
                    Count = 1,
                    Mask = uint.MaxValue
                },
                Fragment = &fragmentState
            };

            RenderPipeline* pipeline = null;
            var validationError = backend.CaptureValidationError(() =>
                pipeline = backend.Api.DeviceCreateRenderPipeline(backend.DeviceHandle, in descriptor));

            if (validationError is not null)
            {
                if (pipeline is not null)
                    backend.Api.RenderPipelineRelease(pipeline);

                throw new InvalidOperationException(
                    $"Unable to create WebGPU render pipeline {Description.Name}: {validationError}");
            }

            return pipeline is not null
                ? pipeline
                : throw new InvalidOperationException($"Unable to create WebGPU render pipeline {Description.Name}");
        }
    }

    ShaderModule* createShaderModule(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source + "\0");
        fixed (byte* sourcePointer = bytes)
        {
            ShaderModuleWGSLDescriptor wgslDescriptor = new()
            {
                Chain = new()
                {
                    SType = SType.ShaderModuleWgslDescriptor
                },
                Code = sourcePointer
            };

            ShaderModuleDescriptor descriptor = new()
            {
                NextInChain = &wgslDescriptor.Chain
            };

            var shader = backend.Api.DeviceCreateShaderModule(backend.DeviceHandle, in descriptor);
            return shader is not null
                ? shader
                : throw new InvalidOperationException($"Unable to create WebGPU shader module {Description.Name}");
        }
    }

    BindGroupLayout* createUniformBindGroupLayout()
    {
        BindGroupLayoutEntry entry = new()
        {
            Binding = 0,
            Visibility = ShaderStage.Vertex,
            Buffer = new()
            {
                Type = BufferBindingType.Uniform,
                HasDynamicOffset = true
            }
        };

        BindGroupLayoutDescriptor descriptor = new()
        {
            EntryCount = 1,
            Entries = &entry
        };

        BindGroupLayout* layout = null;
        var validationError = backend.CaptureValidationError(() =>
            layout = backend.Api.DeviceCreateBindGroupLayout(backend.DeviceHandle, in descriptor));

        if (validationError is not null)
        {
            if (layout is not null)
                backend.Api.BindGroupLayoutRelease(layout);

            throw new InvalidOperationException($"Unable to create WebGPU uniform bind group layout: {validationError}");
        }

        return layout is not null
            ? layout
            : throw new InvalidOperationException("Unable to create WebGPU uniform bind group layout");
    }

    BindGroupLayout* createTextureBindGroupLayout(Backend.PipelineLayout layout)
    {
        var textureBindings = layout.TextureBindings;
        if (textureBindings.Length == 0) return null;

        if (textureBindings.Length > 1)
            throw new NotSupportedException("Core WebGPU renderer currently supports one texture binding layout");

        var capacity = textureBindings[0].Capacity;
        if (backend.UseNativeNonUniformTextureIndexing)
            return createNativeTextureArrayBindGroupLayout(capacity);

        var entries = new BindGroupLayoutEntry[checked(capacity * 2)];
        for (var i = 0; i < capacity; ++i)
        {
            entries[i * 2] = new()
            {
                Binding = (uint)(i * 2),
                Visibility = ShaderStage.Fragment,
                Texture = new()
                {
                    SampleType = TextureSampleType.Float,
                    ViewDimension = TextureViewDimension.Dimension2D
                }
            };

            entries[i * 2 + 1] = new()
            {
                Binding = (uint)(i * 2 + 1),
                Visibility = ShaderStage.Fragment,
                Sampler = new()
                {
                    Type = SamplerBindingType.Filtering
                }
            };
        }

        fixed (BindGroupLayoutEntry* entriesPointer = entries)
        {
            BindGroupLayoutDescriptor descriptor = new()
            {
                EntryCount = (nuint)entries.Length,
                Entries = entriesPointer
            };

            BindGroupLayout* bindGroupLayout = null;
            var validationError = backend.CaptureValidationError(() =>
                bindGroupLayout = backend.Api.DeviceCreateBindGroupLayout(backend.DeviceHandle, in descriptor));

            if (validationError is not null)
            {
                if (bindGroupLayout is not null)
                    backend.Api.BindGroupLayoutRelease(bindGroupLayout);

                throw new InvalidOperationException($"Unable to create WebGPU texture bind group layout: {validationError}");
            }

            return bindGroupLayout is not null
                ? bindGroupLayout
                : throw new InvalidOperationException("Unable to create WebGPU texture bind group layout");
        }
    }

    BindGroupLayout* createNativeTextureArrayBindGroupLayout(int capacity)
    {
        var entries = stackalloc BindGroupLayoutEntry[2];
        WgpuNativeBindGroupLayoutEntryExtras textureExtras = new()
        {
            Chain = new()
            {
                SType = WebGpuNativeExtensions.NativeSType(
                    WebGpuNativeExtensions.STypeBindGroupLayoutEntryExtras)
            },
            Count = (uint)capacity
        };

        entries[0] = new()
        {
            NextInChain = &textureExtras.Chain,
            Binding = 0,
            Visibility = ShaderStage.Fragment,
            Texture = new()
            {
                SampleType = TextureSampleType.Float,
                ViewDimension = TextureViewDimension.Dimension2D
            }
        };

        entries[1] = new()
        {
            Binding = 1,
            Visibility = ShaderStage.Fragment,
            Sampler = new()
            {
                Type = SamplerBindingType.Filtering
            }
        };

        BindGroupLayoutDescriptor descriptor = new()
        {
            EntryCount = 2,
            Entries = entries
        };

        BindGroupLayout* bindGroupLayout = null;
        var validationError = backend.CaptureValidationError(() =>
            bindGroupLayout = backend.Api.DeviceCreateBindGroupLayout(backend.DeviceHandle, in descriptor));

        if (validationError is not null)
        {
            if (bindGroupLayout is not null)
                backend.Api.BindGroupLayoutRelease(bindGroupLayout);

            throw new InvalidOperationException($"Unable to create WebGPU texture array bind group layout: {validationError}");
        }

        return bindGroupLayout is not null
            ? bindGroupLayout
            : throw new InvalidOperationException("Unable to create WebGPU texture array bind group layout");
    }

    PipelineLayout* createPipelineLayout()
    {
        var layoutCount = TextureBindGroupLayout is null ? 1 : 2;
        var layouts = stackalloc BindGroupLayout*[layoutCount];
        layouts[0] = uniformBindGroupLayout;
        if (TextureBindGroupLayout is not null)
            layouts[1] = TextureBindGroupLayout;

        PipelineLayoutDescriptor descriptor = new()
        {
            BindGroupLayoutCount = (nuint)layoutCount,
            BindGroupLayouts = layouts
        };

        PipelineLayout* createdLayout = null;
        var validationError = backend.CaptureValidationError(() =>
            createdLayout = backend.Api.DeviceCreatePipelineLayout(backend.DeviceHandle, in descriptor));

        if (validationError is not null)
        {
            if (createdLayout is not null)
                backend.Api.PipelineLayoutRelease(createdLayout);

            throw new InvalidOperationException($"Unable to create WebGPU pipeline layout: {validationError}");
        }

        return createdLayout is not null
            ? createdLayout
            : throw new InvalidOperationException("Unable to create WebGPU pipeline layout");
    }

    void createUniformBuffer()
    {
        BufferDescriptor bufferDescriptor = new()
        {
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
            Size = UniformBufferSize
        };

        uniformBuffer = backend.Api.DeviceCreateBuffer(backend.DeviceHandle, in bufferDescriptor);
        if (uniformBuffer is null)
            throw new InvalidOperationException("Unable to create WebGPU uniform buffer");

        uniformShadow = GC.AllocateUninitializedArray<byte>(UniformBufferSize);

        BindGroupEntry entry = new()
        {
            Binding = 0,
            Buffer = uniformBuffer,
            Size = UniformBindingSize
        };

        BindGroupDescriptor descriptor = new()
        {
            Layout = uniformBindGroupLayout,
            EntryCount = 1,
            Entries = &entry
        };

        uniformBindGroup = backend.Api.DeviceCreateBindGroup(backend.DeviceHandle, in descriptor);
        if (uniformBindGroup is null)
            throw new InvalidOperationException("Unable to create WebGPU uniform bind group");
    }

    static int align(int value, int alignment)
        => value + alignment - 1 & ~(alignment - 1);

    static WgpuPrimitiveTopology toPrimitiveTopology(PrimitiveTopology topology)
        => topology switch
        {
            PrimitiveTopology.Lines => WgpuPrimitiveTopology.LineList,
            PrimitiveTopology.Triangles => WgpuPrimitiveTopology.TriangleList,
            _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
        };

    static VertexStepMode toStepMode(VertexInputRate inputRate)
        => inputRate switch
        {
            VertexInputRate.Vertex => VertexStepMode.Vertex,
            VertexInputRate.Instance => VertexStepMode.Instance,
            _ => throw new ArgumentOutOfRangeException(nameof(inputRate), inputRate, null)
        };

    static VertexFormat toVertexFormat(VertexAttributeFormat format)
        => format switch
        {
            VertexAttributeFormat.Float32 => VertexFormat.Float32,
            VertexAttributeFormat.Float32x2 or VertexAttributeFormat.Float32Mat3x2 => VertexFormat.Float32x2,
            VertexAttributeFormat.Float32x3 => VertexFormat.Float32x3,
            VertexAttributeFormat.Float32x4 => VertexFormat.Float32x4,
            VertexAttributeFormat.Float16x2 => VertexFormat.Float16x2,
            VertexAttributeFormat.Float16x4 => VertexFormat.Float16x4,
            VertexAttributeFormat.Unorm8x4 => VertexFormat.Unorm8x4,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    static BlendFactor toBlendFactor(Graphics.BlendFactor factor)
        => factor switch
        {
            Graphics.BlendFactor.Zero => BlendFactor.Zero,
            Graphics.BlendFactor.One => BlendFactor.One,
            Graphics.BlendFactor.SrcAlpha => BlendFactor.SrcAlpha,
            Graphics.BlendFactor.OneMinusSrcAlpha => BlendFactor.OneMinusSrcAlpha,
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
        public WgpuBuffer* BoundHandle;
        public int BoundOffset;
    }
}