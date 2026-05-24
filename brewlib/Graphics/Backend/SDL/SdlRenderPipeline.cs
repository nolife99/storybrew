namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Renderers;
using SDL3;
using Shaders;
using Textures;
using Util;

public sealed class SdlRenderPipeline : IRenderPipeline
{
    readonly SdlGraphicsBackend backend;
    readonly SdlGraphicsDevice device;
    readonly Dictionary<BlendingFactorState, nint> graphicsPipelines = [];
    readonly VertexBufferBinding[] vertexBuffers;
    BlendingFactorState boundBlendState;
    uint boundRenderPassSerial = uint.MaxValue;
    bool disposed;
    bool pipelineRebindNeeded = true;

    nint vertexShader, fragmentShader;

    public SdlRenderPipeline(SdlGraphicsBackend backend, RenderPipelineDescription description)
    {
        this.backend = backend;
        device = (SdlGraphicsDevice)backend.Device;
        Description = description;
        vertexBuffers = new VertexBufferBinding[description.VertexInput.Buffers.Length];

        if (description.ShaderSource.Language != ShaderSourceLanguage.Hlsl)
            throw new NotSupportedException(
                "SDL render pipelines currently require HLSL sources so they can be compiled for the active SDL GPU driver");

        (vertexShader, fragmentShader) =
            SdlShaderCompiler.CompileGraphicsShadersFromHlsl(backend.DeviceHandle,
                backend.ShaderFormat,
                description.ShaderSource);
    }

    public GraphicsResourceHandle NativeHandle => new(backend.Name, vertexShader);
    public RenderPipelineDescription Description { get; }

    public IRenderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name)
        => new SdlRenderUniform<T>(this, ShaderBindingStage.Vertex, 0, name.ToString());

    public IRenderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform)
        => new SdlRenderUniform<T>(this, uniform.Stage, uniform.Slot, uniform.Name);

    public IResourceSet CreateResourceSet()
        => new SdlResourceSet(backend, this, Description.PipelineLayout);

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (backend.TryGetReadyRenderPass(out var renderPass)) EnsureBound(renderPass);
        else pipelineRebindNeeded = true;
    }

    public void Unbind() { }

    public void BindVertexBuffer(int slot, IGraphicsBuffer buffer)
        => BindVertexBuffer(slot, buffer, buffer is SdlGraphicsBuffer sdlBuffer ? sdlBuffer.BindingOffset : 0);

    public void BindVertexBuffer(int slot, IGraphicsBuffer buffer, int offset)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), offset, null);

        if (buffer is not SdlGraphicsBuffer sdlBuffer)
            throw new InvalidOperationException($"{nameof(SdlRenderPipeline)} can only bind SDL GPU buffers");

        var buffers = Description.VertexInput.Buffers;
        for (var i = 0; i < buffers.Length; ++i)
            if (buffers[i].Slot == slot)
            {
                vertexBuffers[i].Buffer = sdlBuffer;
                vertexBuffers[i].Offset = offset;
                return;
            }

        throw new ArgumentException($"Vertex buffer slot {slot} is not part of this render pipeline", nameof(slot));
    }

    public void Draw(DrawCommand command)
    {
        if (command.VertexCount == 0) return;

        var renderPass = requireRenderPass();
        if (renderPass == nint.Zero) return;

        EnsureBound(renderPass);
        SDL.DrawGPUPrimitives(renderPass,
            (uint)command.VertexCount,
            1,
            (uint)command.FirstVertex,
            0);
    }

    public void DrawInstanced(DrawInstancedCommand command)
    {
        if (command.VertexCount == 0 || command.InstanceCount == 0) return;

        var renderPass = requireRenderPass();
        if (renderPass == nint.Zero) return;

        EnsureBound(renderPass);
        SDL.DrawGPUPrimitives(renderPass,
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

        if (command.Buffer is not SdlGraphicsBuffer sdlBuffer)
            throw new InvalidOperationException($"{nameof(SdlRenderPipeline)} can only draw from SDL indirect buffers");

        if (sdlBuffer.BufferHandle == nint.Zero)
            return;

        var renderPass = requireRenderPass();
        if (renderPass == nint.Zero) return;

        EnsureBound(renderPass);

        SDL.DrawGPUPrimitivesIndirect(renderPass,
            sdlBuffer.BufferHandle,
            checked((uint)(sdlBuffer.BindingOffset + command.Offset)),
            (uint)command.DrawCount);
    }

    public void Dispose()
    {
        if (disposed) return;

        foreach (var pipeline in graphicsPipelines.Values)
            backend.ReleaseGraphicsPipeline(pipeline);

        backend.ReleaseShader(vertexShader);
        backend.ReleaseShader(fragmentShader);

        graphicsPipelines.Clear();
        vertexShader = nint.Zero;
        fragmentShader = nint.Zero;
        disposed = true;
    }

    internal void PushUniform<T>(ShaderBindingStage stage, uint slot, string name, scoped ref readonly T value)
    {
        var commandBuffer = backend.CommandBuffer;
        if (commandBuffer == nint.Zero)
            throw new InvalidOperationException($"SDL uniform '{name}' cannot be pushed outside an active GPU frame");

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new NotSupportedException($"SDL uniform '{name}' must be an unmanaged value");

        var size = Unsafe.SizeOf<T>();
        var pointer = UnsafeMemory.AsPointerUnconstrained(in value);
        switch (stage)
        {
            case ShaderBindingStage.Vertex:
                SDL.PushGPUVertexUniformData(commandBuffer, slot, pointer, (uint)size);
                break;

            case ShaderBindingStage.Fragment:
                SDL.PushGPUFragmentUniformData(commandBuffer, slot, pointer, (uint)size);
                break;

            default:
                throw new NotSupportedException($"SDL graphics pipelines do not support {stage} uniforms");
        }
    }

    nint getGraphicsPipeline(BlendingFactorState blendState)
    {
        if (graphicsPipelines.TryGetValue(blendState, out var pipeline)) return pipeline;

        pipeline = createGraphicsPipeline(blendState);
        graphicsPipelines.Add(blendState, pipeline);
        return pipeline;
    }

    unsafe nint createGraphicsPipeline(BlendingFactorState blendState)
    {
        if (backend.SwapchainFormat == default)
            throw new InvalidOperationException("SDL graphics pipelines require a claimed window swapchain format");

        var bufferLayouts = Description.VertexInput.Buffers;
        var bufferDescriptions = new SDL.GPUVertexBufferDescription[bufferLayouts.Length];
        var attributeCount = 0;
        for (var i = 0; i < bufferLayouts.Length; ++i)
        {
            var layout = bufferLayouts[i];
            bufferDescriptions[i] = new()
            {
                Slot = (uint)layout.Slot,
                Pitch = (uint)layout.Stride,
                InputRate = toSdlInputRate(layout.InputRate),
                InstanceStepRate = 0
            };

            foreach (var element in layout.Elements)
                attributeCount += element.Format.GetLocationCount();
        }

        var attributes = new SDL.GPUVertexAttribute[attributeCount];
        var attributeIndex = 0;
        for (var i = 0; i < bufferLayouts.Length; ++i)
        {
            var layout = bufferLayouts[i];
            foreach (var element in layout.Elements)
            {
                var locationCount = element.Format.GetLocationCount();
                for (var column = 0; column < locationCount; ++column)
                {
                    attributes[attributeIndex] = new()
                    {
                        Location = (uint)attributeIndex,
                        BufferSlot = (uint)layout.Slot,
                        Format = toSdlVertexElementFormat(element.Format),
                        Offset = (uint)(element.Offset + element.Format.GetLocationOffset(column))
                    };

                    ++attributeIndex;
                }
            }
        }

        SDL.GPUColorTargetDescription[] colorTargetDescriptions =
        [
            new()
            {
                Format = backend.SwapchainFormat,
                BlendState = toSdlBlendState(blendState)
            }
        ];

        fixed (SDL.GPUVertexBufferDescription* bufferDescriptionsPointer = bufferDescriptions)
        fixed (SDL.GPUVertexAttribute* attributesPointer = attributes)
        fixed (SDL.GPUColorTargetDescription* colorTargetsPointer = colorTargetDescriptions)
        {
            var createInfo = new SDL.GPUGraphicsPipelineCreateInfo
            {
                VertexShader = vertexShader,
                FragmentShader = fragmentShader,
                VertexInputState = new()
                {
                    VertexBufferDescriptions = (nint)bufferDescriptionsPointer,
                    NumVertexBuffers = (uint)bufferDescriptions.Length,
                    VertexAttributes = (nint)attributesPointer,
                    NumVertexAttributes = (uint)attributes.Length
                },
                PrimitiveType = toSdlPrimitiveType(Description.Topology),
                RasterizerState = new()
                {
                    FillMode = SDL.GPUFillMode.Fill,
                    CullMode = SDL.GPUCullMode.None,
                    FrontFace = SDL.GPUFrontFace.CounterClockwise
                },
                MultisampleState = new()
                {
                    SampleCount = SDL.GPUSampleCount.SampleCount1
                },
                TargetInfo = new()
                {
                    ColorTargetDescriptions = (nint)colorTargetsPointer,
                    NumColorTargets = 1
                }
            };

            var pipeline = SDL.CreateGPUGraphicsPipeline(backend.DeviceHandle, in createInfo);
            if (pipeline == nint.Zero)
                throw new InvalidOperationException(
                    $"Unable to create SDL graphics pipeline {Description.Name}: {SDL.GetError()}");

            return pipeline;
        }
    }

    nint requireRenderPass()
        => backend.TryGetReadyRenderPass(out var renderPass) ? renderPass : backend.RequireRenderPass();

    internal void EnsureBound(nint renderPass)
    {
        var currentSerial = backend.RenderPassSerial;
        var newPass = boundRenderPassSerial != currentSerial;
        var blendState = device.BlendState;

        if (pipelineRebindNeeded || newPass || boundBlendState != blendState)
        {
            SDL.BindGPUGraphicsPipeline(renderPass, getGraphicsPipeline(blendState));
            boundBlendState = blendState;
            pipelineRebindNeeded = false;
        }

        if (newPass)
        {
            device.ApplyRenderPassState(renderPass);
            boundRenderPassSerial = currentSerial;
        }

        var layouts = Description.VertexInput.Buffers;
        Span<SDL.GPUBufferBinding> bindings = stackalloc SDL.GPUBufferBinding[1];
        for (var i = 0; i < vertexBuffers.Length; ++i)
        {
            ref var binding = ref vertexBuffers[i];
            var sdlBuffer = binding.Buffer;
            if (sdlBuffer is null)
                throw new InvalidOperationException($"Vertex buffer slot {layouts[i].Slot} is not bound");

            var handle = sdlBuffer.BufferHandle;
            if (handle == nint.Zero) continue;

            if (binding.BoundSerial == currentSerial &&
                binding.BoundHandle == handle &&
                binding.BoundOffset == binding.Offset)
                continue;

            bindings[0] = new()
            {
                Buffer = handle,
                Offset = (uint)binding.Offset
            };

            SDL.BindGPUVertexBuffers(renderPass, (uint)layouts[i].Slot, bindings.AsPointer(), 1);

            binding.BoundSerial = currentSerial;
            binding.BoundHandle = handle;
            binding.BoundOffset = binding.Offset;
        }
    }

    static SDL.GPUColorTargetBlendState toSdlBlendState(BlendingFactorState state)
        => new()
        {
            SrcColorBlendfactor = toSdlBlendFactor(state.Source),
            DstColorBlendfactor = toSdlBlendFactor(state.Destination),
            ColorBlendOp = SDL.GPUBlendOp.Add,
            SrcAlphaBlendfactor = toSdlBlendFactor(state.AlphaSource),
            DstAlphaBlendfactor = toSdlBlendFactor(state.AlphaDestination),
            AlphaBlendOp = SDL.GPUBlendOp.Add,
            ColorWriteMask = SDL.GPUColorComponentFlags.R |
                SDL.GPUColorComponentFlags.G |
                SDL.GPUColorComponentFlags.B |
                SDL.GPUColorComponentFlags.A,
            EnableBlend = state.Enabled ? (byte)1 : (byte)0,
            EnableColorWriteMask = 1
        };

    static SDL.GPUBlendFactor toSdlBlendFactor(BlendFactor factor)
        => factor switch
        {
            BlendFactor.Zero => SDL.GPUBlendFactor.Zero,
            BlendFactor.One => SDL.GPUBlendFactor.One,
            BlendFactor.SrcAlpha => SDL.GPUBlendFactor.SrcAlpha,
            BlendFactor.OneMinusSrcAlpha => SDL.GPUBlendFactor.OneMinusSrcAlpha,
            _ => throw new ArgumentOutOfRangeException(nameof(factor), factor, null)
        };

    static SDL.GPUPrimitiveType toSdlPrimitiveType(PrimitiveTopology topology)
        => topology switch
        {
            PrimitiveTopology.Points => SDL.GPUPrimitiveType.PointList,
            PrimitiveTopology.Lines => SDL.GPUPrimitiveType.LineList,
            PrimitiveTopology.Triangles => SDL.GPUPrimitiveType.TriangleList,
            _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
        };

    static SDL.GPUVertexInputRate toSdlInputRate(VertexInputRate inputRate)
        => inputRate switch
        {
            VertexInputRate.Vertex => SDL.GPUVertexInputRate.Vertex,
            VertexInputRate.Instance => SDL.GPUVertexInputRate.Instance,
            _ => throw new ArgumentOutOfRangeException(nameof(inputRate), inputRate, null)
        };

    static SDL.GPUVertexElementFormat toSdlVertexElementFormat(VertexAttributeFormat format)
        => format switch
        {
            VertexAttributeFormat.Float32 => SDL.GPUVertexElementFormat.Float,
            VertexAttributeFormat.Float32x2 => SDL.GPUVertexElementFormat.Float2,
            VertexAttributeFormat.Float32x3 => SDL.GPUVertexElementFormat.Float3,
            VertexAttributeFormat.Float32x4 => SDL.GPUVertexElementFormat.Float4,
            VertexAttributeFormat.Float32Mat3x2 => SDL.GPUVertexElementFormat.Float2,
            VertexAttributeFormat.Float16x2 => SDL.GPUVertexElementFormat.Half2,
            VertexAttributeFormat.Float16x4 => SDL.GPUVertexElementFormat.Half4,
            VertexAttributeFormat.Unorm8x4 => SDL.GPUVertexElementFormat.Ubyte4Norm,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    readonly struct SdlRenderUniform<T>(
        SdlRenderPipeline pipeline,
        ShaderBindingStage stage,
        uint slot,
        string name) : IRenderUniform<T>
    {
        public void SetValue(T value) => pipeline.PushUniform(stage, slot, name, in value);
    }

    struct VertexBufferBinding
    {
        public SdlGraphicsBuffer Buffer;
        public int Offset;
        public uint BoundSerial;
        public nint BoundHandle;
        public int BoundOffset;
    }
}

public sealed class SdlResourceSet : IResourceSet
{
    readonly SdlGraphicsBackend backend;
    readonly SdlRenderPipeline pipeline;
    readonly TextureBinding[] textureBindings;
    bool disposed;

    public SdlResourceSet(SdlGraphicsBackend backend, SdlRenderPipeline pipeline, PipelineLayout layout)
    {
        this.backend = backend;
        this.pipeline = pipeline;

        var pipelineTextureBindings = layout.TextureBindings;
        textureBindings = new TextureBinding[pipelineTextureBindings.Length];
        for (var i = 0; i < pipelineTextureBindings.Length; ++i)
        {
            var textureBinding = pipelineTextureBindings[i];
            textureBindings[i] = new(textureBinding.Binding, textureBinding.Capacity);
        }
    }

    public void SetTextures(int binding, scoped ReadOnlySpan<ITexture> textures)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        ref var textureBinding = ref getTextureBinding(binding);
        if (textures.Length > textureBinding.Textures.Length)
            throw new ArgumentException(
                $"Texture binding {binding} accepts at most {textureBinding.Textures.Length} textures",
                nameof(textures));

        var changed = textureBinding.Count != textures.Length;
        for (var i = 0; i < textures.Length; ++i)
        {
            if (textures[i] is not SdlTexture texture)
                throw new InvalidOperationException($"{nameof(SdlResourceSet)} can only bind SDL textures");

            changed |= !ReferenceEquals(textureBinding.Textures[i], texture);
            textureBinding.Textures[i] = texture;
        }

        if (textureBinding.Count > textures.Length)
            Array.Clear(textureBinding.Textures, textures.Length, textureBinding.Count - textures.Length);

        textureBinding.Count = textures.Length;
        if (changed)
            ++textureBinding.Version;
    }

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var renderPass = backend.TryGetReadyRenderPass(out var readyRenderPass)
            ? readyRenderPass
            : backend.RequireRenderPass();

        if (renderPass == nint.Zero) return;

        pipeline.EnsureBound(renderPass);

        var currentSerial = backend.RenderPassSerial;

        var maxCount = 0;
        for (var i = 0; i < textureBindings.Length; ++i)
            maxCount = int.Max(maxCount, textureBindings[i].Count);

        var samplerBindings = maxCount <= 0 ?
            [] :
            stackalloc SDL.GPUTextureSamplerBinding[maxCount];

        for (var i = 0; i < textureBindings.Length; ++i)
        {
            ref var textureBinding = ref textureBindings[i];
            var count = textureBinding.Count;
            if (count == 0) continue;
            if (textureBinding.BoundSerial == currentSerial &&
                textureBinding.BoundVersion == textureBinding.Version)
                continue;

            var bindingSpan = samplerBindings[..count];
            var textures = textureBinding.Textures;
            for (var j = 0; j < count; ++j)
            {
                var texture = textures[j];
                bindingSpan[j] = new()
                {
                    Texture = texture.TextureHandle,
                    Sampler = texture.SamplerHandle
                };
            }

            SDL.BindGPUFragmentSamplers(renderPass,
                (uint)textureBinding.Binding,
                bindingSpan.AsPointer(),
                (uint)count);

            textureBinding.BoundSerial = currentSerial;
            textureBinding.BoundVersion = textureBinding.Version;
        }
    }

    public void Dispose()
    {
        if (disposed) return;

        for (var i = 0; i < textureBindings.Length; ++i)
            Array.Clear(textureBindings[i].Textures, 0, textureBindings[i].Count);

        disposed = true;
    }

    ref TextureBinding getTextureBinding(int binding)
    {
        for (var i = 0; i < textureBindings.Length; ++i)
            if (textureBindings[i].Binding == binding)
                return ref textureBindings[i];

        throw new ArgumentException($"Texture binding {binding} is not part of this resource set", nameof(binding));
    }

    struct TextureBinding
    {
        public TextureBinding(int binding, int capacity)
        {
            Binding = binding;
            Textures = new SdlTexture[capacity];
            BoundSerial = uint.MaxValue;
        }

        public readonly int Binding;
        public readonly SdlTexture[] Textures;
        public int Count;
        public uint Version;
        public uint BoundSerial;
        public uint BoundVersion;
    }
}

public sealed class SdlRenderPipelineFactory(SdlGraphicsBackend backend) : IRenderPipelineFactory
{
    public IRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
        => new SdlRenderPipeline(backend, description);
}