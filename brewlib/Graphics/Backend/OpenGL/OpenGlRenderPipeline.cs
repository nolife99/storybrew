namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Renderers;
using Shaders;
using Silk.NET.OpenGL;
using Shader = Shaders.Shader;

public sealed class OpenGlRenderPipeline : IRenderPipeline
{
    readonly OpenGlGraphicsDevice device;
    readonly Shader shader;
    readonly OpenGlTextureBindingInfo[] textureBindings;
    readonly List<IDisposable> uniforms = [];
    readonly uint vertexArrayId;
    readonly OpenGlVertexBufferBinding[] vertexBuffers;

    bool bound, disposed;

    public OpenGlRenderPipeline(OpenGlGraphicsBackend backend,
        OpenGlGraphicsDevice device,
        RenderPipelineDescription description)
    {
        this.device = device;
        Description = description;

        var shaderSource = OpenGlShaderCompiler.CreateShaderSource(description,
            backend.GlslVersion,
            backend.GlslEs);

        shader = new(shaderSource, backend);
        vertexArrayId = OpenGlApi.GL.GenVertexArray();

        var pipelineTextureBindings = description.PipelineLayout.TextureBindings;
        textureBindings = new OpenGlTextureBindingInfo[pipelineTextureBindings.Length];
        for (var i = 0; i < pipelineTextureBindings.Length; ++i)
        {
            var binding = pipelineTextureBindings[i];
            var location = shader.GetUniformLocation(binding.Name, binding.Capacity > 1 ? 0 : -1);
            textureBindings[i] = new(binding.Binding, binding.Name, binding.Capacity, location);
        }

        var vertexInputBuffers = description.VertexInput.Buffers;
        vertexBuffers = new OpenGlVertexBufferBinding[vertexInputBuffers.Length];
        for (var i = 0; i < vertexInputBuffers.Length; ++i)
            vertexBuffers[i] = new(vertexInputBuffers[i]);
    }

    internal ReadOnlySpan<OpenGlTextureBindingInfo> TextureBindings => textureBindings;

    public GraphicsResourceHandle NativeHandle => shader.NativeHandle;
    public RenderPipelineDescription Description { get; }

    public IRenderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name)
        => new OpenGlRenderUniform<T>(shader.GetUniform<T>(name));

    public IRenderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform)
    {
        var renderUniform = new OpenGlUniformBufferRenderUniform<T>(uniform);
        uniforms.Add(renderUniform);
        return renderUniform;
    }

    public IResourceSet CreateResourceSet()
        => new OpenGlResourceSet(device, this);

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (!bound)
        {
            shader.Begin();
            bound = true;
        }

        OpenGlApi.GL.BindVertexArray(vertexArrayId);
    }

    public void Unbind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!bound) return;

        OpenGlApi.GL.BindVertexArray(0);
        shader.End();
        bound = false;
    }

    public void BindVertexBuffer(int slot, IGraphicsBuffer buffer)
        => BindVertexBuffer(slot, buffer, 0);

    public void BindVertexBuffer(int slot, IGraphicsBuffer buffer, int offset)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), offset, null);

        if (buffer is not OpenGlGraphicsBuffer openGlBuffer || openGlBuffer.Target != BufferTargetARB.ArrayBuffer)
            throw new InvalidOperationException($"{nameof(OpenGlRenderPipeline)} can only bind OpenGL vertex buffers");

        ref var binding = ref getVertexBuffer(slot);
        if (binding.BufferId == openGlBuffer.BufferId && binding.BufferOffset == offset) return;

        OpenGlApi.GL.BindVertexArray(vertexArrayId);
        openGlBuffer.Bind();

        foreach (var element in binding.Layout.Elements)
        {
            var location = shader.GetAttributeLocation(element.Name);
            if (location < 0) continue;

            var locationCount = element.Format.GetLocationCount();
            for (var column = 0; column < locationCount; ++column)
            {
                var columnLocation = location + column;
                OpenGlApi.GL.EnableVertexAttribArray((uint)columnLocation);
                OpenGlApi.GL.VertexAttribPointer((uint)columnLocation,
                    element.Format.GetComponentCount(),
                    toOpenGlVertexAttributeType(element.Format),
                    element.Format.IsNormalized(),
                    (uint)binding.Layout.Stride,
                    offset + element.Offset + element.Format.GetLocationOffset(column));

                OpenGlApi.GL.VertexAttribDivisor((uint)columnLocation, binding.Layout.InputRate == VertexInputRate.Instance ? 1u : 0u);
            }
        }

        binding.BufferId = openGlBuffer.BufferId;
        binding.BufferOffset = offset;
        if (!bound) OpenGlApi.GL.BindVertexArray(0);
    }

    public void Draw(DrawCommand command)
    {
        if (command.VertexCount == 0) return;

        Bind();
        OpenGlApi.GL.DrawArrays(toOpenGlPrimitiveType(Description.Topology), command.FirstVertex, (uint)command.VertexCount);

        DrawState.CountDrawCall();
    }

    public void DrawInstanced(DrawInstancedCommand command)
    {
        if (command.VertexCount == 0 || command.InstanceCount == 0) return;

        Bind();
        OpenGlApi.GL.DrawArraysInstanced(toOpenGlPrimitiveType(Description.Topology),
            command.FirstVertex,
            (uint)command.VertexCount,
            (uint)command.InstanceCount);

        DrawState.CountDrawCall();
    }

    public unsafe void DrawIndirect(DrawIndirectCommand command)
    {
        if (command.DrawCount == 0) return;

        if (command.Offset < 0)
            throw new ArgumentOutOfRangeException(nameof(command), command.Offset, "Offset must be non-negative.");

        if (command.DrawCount < 0)
            throw new ArgumentOutOfRangeException(nameof(command), command.DrawCount, "Draw count must be non-negative.");

        if (command.Buffer is not OpenGlGraphicsBuffer openGlBuffer ||
            openGlBuffer.Target != BufferTargetARB.DrawIndirectBuffer)
            throw new InvalidOperationException($"{nameof(OpenGlRenderPipeline)} can only draw from OpenGL indirect buffers");

        DrawState.CountDrawCall();

        Bind();
        openGlBuffer.Bind();

        var offset = (void*)command.Offset;
        if (command.DrawCount == 1)
        {
            OpenGlApi.GL.DrawArraysIndirect(toOpenGlPrimitiveType(Description.Topology), offset);
            return;
        }

        OpenGlApi.GL.MultiDrawArraysIndirect(toOpenGlPrimitiveType(Description.Topology),
            offset,
            (uint)command.DrawCount,
            (uint)Unsafe.SizeOf<IndirectDrawCommand>());
    }

    public void Dispose()
    {
        if (disposed) return;

        if (bound) Unbind();

        OpenGlApi.GL.DeleteVertexArray(vertexArrayId);
        foreach (var uniform in uniforms)
            uniform.Dispose();

        uniforms.Clear();
        shader.Dispose();
        disposed = true;
    }

    ref OpenGlVertexBufferBinding getVertexBuffer(int slot)
    {
        for (var i = 0; i < vertexBuffers.Length; ++i)
            if (vertexBuffers[i].Layout.Slot == slot)
                return ref vertexBuffers[i];

        throw new ArgumentException($"Vertex buffer slot {slot} is not part of this render pipeline", nameof(slot));
    }

    static VertexAttribPointerType toOpenGlVertexAttributeType(VertexAttributeFormat format)
        => format switch
        {
            VertexAttributeFormat.Float32 or
                VertexAttributeFormat.Float32x2 or
                VertexAttributeFormat.Float32x3 or
                VertexAttributeFormat.Float32x4 or
                VertexAttributeFormat.Float32Mat3x2 => VertexAttribPointerType.Float,
            VertexAttributeFormat.Float16x2 or VertexAttributeFormat.Float16x4 => VertexAttribPointerType.HalfFloat,
            VertexAttributeFormat.Unorm8x4 => VertexAttribPointerType.UnsignedByte,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    static PrimitiveType toOpenGlPrimitiveType(PrimitiveTopology topology)
        => topology switch
        {
            PrimitiveTopology.Points => PrimitiveType.Points,
            PrimitiveTopology.Lines => PrimitiveType.Lines,
            PrimitiveTopology.Triangles => PrimitiveType.Triangles,
            _ => throw new ArgumentOutOfRangeException(nameof(topology), topology, null)
        };

    readonly struct OpenGlRenderUniform<T>(ShaderUniform<T> uniform) : IRenderUniform<T>
    {
        public void SetValue(T value) => uniform.Set(value);
    }

    sealed class OpenGlUniformBufferRenderUniform<T>(ShaderUniformBinding<T> uniform) : IRenderUniform<T>, IDisposable
    {
        uint bufferId;
        bool disposed;

        public void Dispose()
        {
            if (disposed) return;

            if (bufferId != 0) OpenGlApi.GL.DeleteBuffer(bufferId);
            bufferId = 0;
            disposed = true;
        }

        public unsafe void SetValue(T value)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                throw new NotSupportedException($"OpenGL uniform '{uniform.Name}' must be an unmanaged value");

            bufferId = bufferId == 0 ? OpenGlApi.GL.GenBuffer() : bufferId;

            var size = Unsafe.SizeOf<T>();
            OpenGlApi.GL.BindBuffer(BufferTargetARB.UniformBuffer, bufferId);
            OpenGlApi.GL.BufferData(BufferTargetARB.UniformBuffer,
                (nuint)size,
                Unsafe.AsPointer(ref value),
                BufferUsageARB.DynamicDraw);

            OpenGlApi.GL.BindBufferBase(BufferTargetARB.UniformBuffer, uniform.Slot, bufferId);
        }
    }

    struct OpenGlVertexBufferBinding
    {
        public OpenGlVertexBufferBinding(VertexBufferLayout layout)
        {
            Layout = layout;
            BufferId = 0;
            BufferOffset = 0;
        }

        public VertexBufferLayout Layout { get; }
        public uint BufferId { get; set; }
        public int BufferOffset { get; set; }
    }
}

public sealed class OpenGlRenderPipelineFactory(OpenGlGraphicsBackend backend, OpenGlGraphicsDevice device)
    : IRenderPipelineFactory
{
    public IRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
        => new OpenGlRenderPipeline(backend, device, description);
}

readonly record struct OpenGlTextureBindingInfo(
    int Binding,
    string Name,
    int Capacity,
    int Location);