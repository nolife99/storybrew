namespace BrewLib.Graphics.Backend.SDL;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Shaders;
using BrewLib.Graphics.Textures;
using SDL3;

public sealed class SdlRenderPipeline : IRenderPipeline
{
    readonly SdlGraphicsBackend backend;
    readonly SdlGraphicsDevice device;
    readonly IGraphicsBuffer[] vertexBuffers;
    readonly Dictionary<BlendingFactorState, nint> graphicsPipelines = [];

    nint vertexShader, fragmentShader;
    bool disposed;

    public SdlRenderPipeline(SdlGraphicsBackend backend, RenderPipelineDescription description)
    {
        this.backend = backend;
        device = (SdlGraphicsDevice)backend.Device;
        Description = description;
        vertexBuffers = new IGraphicsBuffer[description.VertexInput.Buffers.Length];

        if (description.ShaderSource.Language != ShaderSourceLanguage.Hlsl)
            throw new NotSupportedException(
                "SDL render pipelines currently require HLSL sources so SDL_shadercross can compile them at runtime");

        vertexShader = SdlShaderCross.CompileGraphicsShaderFromHlsl(backend.DeviceHandle,
            description.ShaderSource.Name + ".Vertex",
            CompiledShaderStage.Vertex,
            description.ShaderSource.VertexSource,
            description.ShaderSource.VertexEntryPoint);

        fragmentShader = SdlShaderCross.CompileGraphicsShaderFromHlsl(backend.DeviceHandle,
            description.ShaderSource.Name + ".Fragment",
            CompiledShaderStage.Fragment,
            description.ShaderSource.FragmentSource,
            description.ShaderSource.FragmentEntryPoint);
    }

    public GraphicsResourceHandle NativeHandle => new(backend.Name, vertexShader);
    public RenderPipelineDescription Description { get; }

    public IRenderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name)
        => new SdlRenderUniform<T>(this, ShaderBindingStage.Vertex, 0, name.ToString());

    public IRenderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform)
        => new SdlRenderUniform<T>(this, uniform.Stage, uniform.Slot, uniform.Name);

    public IResourceSet CreateResourceSet()
        => new SdlResourceSet(backend, Description.PipelineLayout);

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var renderPass = requireRenderPass();
        SDL.BindGPUGraphicsPipeline(renderPass, getGraphicsPipeline(device.BlendState));
        bindVertexBuffers(renderPass);
        device.ApplyRenderPassState(renderPass);
    }

    public void Unbind()
    {
    }

    public void BindVertexBuffer(int slot, IGraphicsBuffer buffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (buffer is not SdlGraphicsBuffer)
            throw new InvalidOperationException($"{nameof(SdlRenderPipeline)} can only bind SDL GPU buffers");

        var buffers = Description.VertexInput.Buffers;
        for (var i = 0; i < buffers.Length; ++i)
            if (buffers[i].Slot == slot)
            {
                vertexBuffers[i] = buffer;
                return;
            }

        throw new ArgumentException($"Vertex buffer slot {slot} is not part of this render pipeline", nameof(slot));
    }

    public void Draw(DrawCommand command)
    {
        if (command.VertexCount == 0) return;

        // Don't rebind - pipeline and resources are already bound by BeginRendering() and Flush()
        // Rebinding here would invalidate texture bindings set in Flush()
        SDL.DrawGPUPrimitives(requireRenderPass(),
            (uint)command.VertexCount,
            1,
            (uint)command.FirstVertex,
            0);
    }

    public void DrawInstanced(DrawInstancedCommand command)
    {
        if (command.VertexCount == 0 || command.InstanceCount == 0) return;

        // Don't rebind - pipeline and resources are already bound by BeginRendering() and Flush()
        // Rebinding here would invalidate texture bindings set in Flush()
        SDL.DrawGPUPrimitives(requireRenderPass(),
            (uint)command.VertexCount,
            (uint)command.InstanceCount,
            (uint)command.FirstVertex,
            0);
    }

    public void Dispose()
    {
        if (disposed) return;

        foreach (var pipeline in graphicsPipelines.Values)
            SDL.ReleaseGPUGraphicsPipeline(backend.DeviceHandle, pipeline);

        if (vertexShader != nint.Zero) SDL.ReleaseGPUShader(backend.DeviceHandle, vertexShader);
        if (fragmentShader != nint.Zero) SDL.ReleaseGPUShader(backend.DeviceHandle, fragmentShader);

        graphicsPipelines.Clear();
        vertexShader = nint.Zero;
        fragmentShader = nint.Zero;
        disposed = true;
    }

    internal void PushUniform<T>(ShaderBindingStage stage, uint slot, string name, T value)
    {
        var commandBuffer = backend.CommandBuffer;
        if (commandBuffer == nint.Zero)
            throw new InvalidOperationException($"SDL uniform '{name}' cannot be pushed outside an active GPU frame");

        var size = Marshal.SizeOf<T>();
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, pointer, false);
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
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    nint getGraphicsPipeline(BlendingFactorState blendState)
    {
        if (graphicsPipelines.TryGetValue(blendState, out var pipeline)) return pipeline;

        pipeline = createGraphicsPipeline(blendState);
        graphicsPipelines.Add(blendState, pipeline);
        return pipeline;
    }

    nint createGraphicsPipeline(BlendingFactorState blendState)
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
            attributeCount += layout.Elements.Length;
        }

        var attributes = new SDL.GPUVertexAttribute[attributeCount];
        var attributeIndex = 0;
        for (var i = 0; i < bufferLayouts.Length; ++i)
        {
            var layout = bufferLayouts[i];
            foreach (var element in layout.Elements)
            {
                attributes[attributeIndex] = new()
                {
                    Location = (uint)attributeIndex,
                    BufferSlot = (uint)layout.Slot,
                    Format = toSdlVertexElementFormat(element.Format),
                    Offset = (uint)element.Offset
                };
                ++attributeIndex;
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

        var bufferDescriptionsHandle = GCHandle.Alloc(bufferDescriptions, GCHandleType.Pinned);
        var attributesHandle = GCHandle.Alloc(attributes, GCHandleType.Pinned);
        var colorTargetsHandle = GCHandle.Alloc(colorTargetDescriptions, GCHandleType.Pinned);
        try
        {
            var createInfo = new SDL.GPUGraphicsPipelineCreateInfo
            {
                VertexShader = vertexShader,
                FragmentShader = fragmentShader,
                VertexInputState = new()
                {
                    VertexBufferDescriptions = bufferDescriptionsHandle.AddrOfPinnedObject(),
                    NumVertexBuffers = (uint)bufferDescriptions.Length,
                    VertexAttributes = attributesHandle.AddrOfPinnedObject(),
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
                    ColorTargetDescriptions = colorTargetsHandle.AddrOfPinnedObject(),
                    NumColorTargets = 1
                }
            };

            var pipeline = SDL.CreateGPUGraphicsPipeline(backend.DeviceHandle, in createInfo);
            if (pipeline == nint.Zero)
                throw new InvalidOperationException(
                    $"Unable to create SDL graphics pipeline {Description.Name}: {SDL.GetError()}");

            return pipeline;
        }
        finally
        {
            if (colorTargetsHandle.IsAllocated) colorTargetsHandle.Free();
            if (attributesHandle.IsAllocated) attributesHandle.Free();
            if (bufferDescriptionsHandle.IsAllocated) bufferDescriptionsHandle.Free();
        }
    }

    void bindVertexBuffers(nint renderPass)
    {
        var layouts = Description.VertexInput.Buffers;
        for (var i = 0; i < vertexBuffers.Length; ++i)
        {
            if (vertexBuffers[i] is not SdlGraphicsBuffer buffer)
                throw new InvalidOperationException($"Vertex buffer slot {layouts[i].Slot} is not bound");

            if (buffer.BufferHandle == nint.Zero)
                continue;

            SDL.GPUBufferBinding[] bindings =
            [
                new() { Buffer = buffer.BufferHandle }
            ];
            SDL.BindGPUVertexBuffers(renderPass, (uint)layouts[i].Slot, bindings, 1);
        }
    }

    nint requireRenderPass()
    {
        var renderPass = backend.RenderPass;
        if (renderPass == nint.Zero)
            throw new InvalidOperationException("SDL draw submission requires an active GPU render pass");

        return renderPass;
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
        public void SetValue(T value) => pipeline.PushUniform(stage, slot, name, value);
    }
}

public sealed class SdlResourceSet : IResourceSet
{
    readonly SdlGraphicsBackend backend;
    readonly TextureBindingLayout[] textureBindings;
    readonly ITexture[][] textureSets;
    bool disposed;

    public SdlResourceSet(SdlGraphicsBackend backend, PipelineLayout layout)
    {
        this.backend = backend;
        textureBindings = layout.TextureBindings.ToArray();
        textureSets = new ITexture[textureBindings.Length][];
    }

    public void SetTextures(int binding, scoped ReadOnlySpan<ITexture> textures)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var textureBindingIndex = getTextureBindingIndex(binding);
        var textureBinding = textureBindings[textureBindingIndex];
        if (textures.Length > textureBinding.Capacity)
            throw new ArgumentException(
                $"Texture binding {binding} accepts at most {textureBinding.Capacity} textures",
                nameof(textures));

        var targetTextures = new ITexture[textures.Length];
        textures.CopyTo(targetTextures);
        textureSets[textureBindingIndex] = targetTextures;
    }

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var renderPass = backend.RenderPass;
        if (renderPass == nint.Zero)
            throw new InvalidOperationException("SDL resource binding requires an active GPU render pass");

        for (var i = 0; i < textureBindings.Length; ++i)
        {
            var textures = textureSets[i];
            if (textures is null || textures.Length == 0) continue;

            var samplerBindings = new SDL.GPUTextureSamplerBinding[textures.Length];
            for (var j = 0; j < textures.Length; ++j)
            {
                if (textures[j] is not SdlTexture texture)
                    throw new InvalidOperationException($"{nameof(SdlResourceSet)} can only bind SDL textures");

                samplerBindings[j] = new()
                {
                    Texture = texture.TextureHandle,
                    Sampler = texture.SamplerHandle
                };
            }

            SDL.BindGPUFragmentSamplers(renderPass,
                (uint)textureBindings[i].Binding,
                samplerBindings,
                (uint)samplerBindings.Length);
        }
    }

    public void Dispose()
    {
        if (disposed) return;

        for (var i = 0; i < textureSets.Length; ++i) textureSets[i] = null;
        disposed = true;
    }

    int getTextureBindingIndex(int binding)
    {
        for (var i = 0; i < textureBindings.Length; ++i)
            if (textureBindings[i].Binding == binding)
                return i;

        throw new ArgumentException($"Texture binding {binding} is not part of this resource set", nameof(binding));
    }

}

public sealed class SdlRenderPipelineFactory(SdlGraphicsBackend backend) : IRenderPipelineFactory
{
    public IRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
        => new SdlRenderPipeline(backend, description);
}
