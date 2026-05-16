namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Renderers;
using BrewLib.Graphics.Shaders;
using osuTK.Graphics.OpenGL;

public sealed class OpenGlRenderPipeline : IRenderPipeline
{
    readonly OpenGlGraphicsDevice device;
    readonly Shader shader;
    readonly OpenGlTextureBindingInfo[] textureBindings;
    readonly OpenGlVertexBufferBinding[] vertexBuffers;
    readonly int vertexArrayId;

    bool bound, disposed;

    public OpenGlRenderPipeline(OpenGlGraphicsBackend backend,
        OpenGlGraphicsDevice device,
        RenderPipelineDescription description)
    {
        this.device = device;
        Description = description;

        shader = new(description.ShaderSource, backend);
        vertexArrayId = GL.GenVertexArray();

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

    public GraphicsResourceHandle NativeHandle => shader.NativeHandle;
    public RenderPipelineDescription Description { get; }

    internal ReadOnlySpan<OpenGlTextureBindingInfo> TextureBindings => textureBindings;

    public IRenderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name)
        => new OpenGlRenderUniform<T>(shader.GetUniform<T>(name));

    public IRenderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform)
        => new OpenGlRenderUniform<T>(shader.GetUniform(uniform));

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

        GL.BindVertexArray(vertexArrayId);
    }

    public void Unbind()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!bound) return;

        GL.BindVertexArray(0);
        shader.End();
        bound = false;
    }

    public void BindVertexBuffer(int slot, IGraphicsBuffer buffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        if (buffer is not OpenGlGraphicsBuffer openGlBuffer || openGlBuffer.Target != BufferTarget.ArrayBuffer)
            throw new InvalidOperationException($"{nameof(OpenGlRenderPipeline)} can only bind OpenGL vertex buffers");

        ref var binding = ref getVertexBuffer(slot);
        if (binding.BufferId == openGlBuffer.BufferId) return;

        GL.BindVertexArray(vertexArrayId);
        openGlBuffer.Bind();

        foreach (var element in binding.Layout.Elements)
        {
            var location = shader.GetAttributeLocation(element.Name);
            if (location < 0) continue;

            GL.EnableVertexAttribArray(location);
            GL.VertexAttribPointer(location,
                element.Format.GetComponentCount(),
                toOpenGlVertexAttributeType(element.Format),
                element.Format.IsNormalized(),
                binding.Layout.Stride,
                element.Offset);

            GL.VertexAttribDivisor(location, binding.Layout.InputRate == VertexInputRate.Instance ? 1 : 0);
        }

        binding.BufferId = openGlBuffer.BufferId;
        if (!bound) GL.BindVertexArray(0);
    }

    public void Draw(DrawCommand command)
    {
        if (command.VertexCount == 0) return;

        Bind();
        GL.DrawArrays(toOpenGlPrimitiveType(Description.Topology), command.FirstVertex, command.VertexCount);
    }

    public void DrawInstanced(DrawInstancedCommand command)
    {
        if (command.VertexCount == 0 || command.InstanceCount == 0) return;

        Bind();
        GL.DrawArraysInstanced(toOpenGlPrimitiveType(Description.Topology),
            command.FirstVertex,
            command.VertexCount,
            command.InstanceCount);
    }

    public void Dispose()
    {
        if (disposed) return;

        if (bound) Unbind();

        GL.DeleteVertexArray(vertexArrayId);
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
                VertexAttributeFormat.Float32x4 => VertexAttribPointerType.Float,
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

    struct OpenGlVertexBufferBinding
    {
        public OpenGlVertexBufferBinding(VertexBufferLayout layout)
        {
            Layout = layout;
            BufferId = -1;
        }

        public VertexBufferLayout Layout { get; }
        public int BufferId { get; set; }
    }
}

public sealed class OpenGlRenderPipelineFactory(OpenGlGraphicsBackend backend, OpenGlGraphicsDevice device)
    : IRenderPipelineFactory
{
    public IRenderPipeline CreateRenderPipeline(RenderPipelineDescription description)
        => new OpenGlRenderPipeline(backend, device, description);
}

internal readonly record struct OpenGlTextureBindingInfo(
    int Binding,
    string Name,
    int Capacity,
    int Location);
