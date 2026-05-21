namespace BrewLib.Graphics.Backend;

using System;
using System.Runtime.InteropServices;
using Renderers;
using Shaders;
using Textures;

public interface IRenderPipeline : IDisposable
{
    GraphicsResourceHandle NativeHandle { get; }
    RenderPipelineDescription Description { get; }

    IRenderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name);
    IRenderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform);
    IResourceSet CreateResourceSet();

    void Bind();
    void Unbind();
    void BindVertexBuffer(int slot, IGraphicsBuffer buffer);
    void BindVertexBuffer(int slot, IGraphicsBuffer buffer, int offset);
    void Draw(DrawCommand command);
    void DrawInstanced(DrawInstancedCommand command);
    void DrawIndirect(DrawIndirectCommand command);
}

public interface IRenderPipelineFactory
{
    IRenderPipeline CreateRenderPipeline(RenderPipelineDescription description);
}

public interface IRenderUniform<in T>
{
    void SetValue(T value);
}

public interface IResourceSet : IDisposable
{
    void SetTextures(int binding, scoped ReadOnlySpan<ITexture> textures);
    void Bind();
}

public sealed class RenderPipelineDescription(
    string name,
    ShaderProgramSource shaderSource,
    PipelineLayout pipelineLayout,
    VertexInputLayout vertexInput,
    PrimitiveTopology topology)
{
    public string Name { get; } = name;
    public ShaderProgramSource ShaderSource { get; } = shaderSource;
    public PipelineLayout PipelineLayout { get; } = pipelineLayout;
    public VertexInputLayout VertexInput { get; } = vertexInput;
    public PrimitiveTopology Topology { get; } = topology;
}

public sealed class PipelineLayout(params TextureBindingLayout[] textureBindings)
{
    public ReadOnlySpan<TextureBindingLayout> TextureBindings => textureBindings;

    public TextureBindingLayout GetTextureBinding(int binding)
    {
        foreach (var textureBinding in textureBindings)
            if (textureBinding.Binding == binding)
                return textureBinding;

        throw new ArgumentException($"Texture binding {binding} is not part of this pipeline layout", nameof(binding));
    }
}

public readonly record struct TextureBindingLayout
{
    public TextureBindingLayout(int binding, string name, int capacity)
        : this(binding, new ShaderSamplerBinding(name), capacity)
    {
    }

    public TextureBindingLayout(int binding, ShaderSamplerBinding sampler, int capacity)
    {
        Binding = binding;
        Sampler = sampler;
        Capacity = capacity;
    }

    public int Binding { get; }
    public ShaderSamplerBinding Sampler { get; }
    public string Name => Sampler.Name;
    public int Capacity { get; }
}

public sealed class VertexInputLayout(params VertexBufferLayout[] buffers)
{
    public ReadOnlySpan<VertexBufferLayout> Buffers => buffers;

    public VertexBufferLayout GetBuffer(int slot)
    {
        foreach (var buffer in buffers)
            if (buffer.Slot == slot)
                return buffer;

        throw new ArgumentException($"Vertex buffer slot {slot} is not part of this vertex input layout", nameof(slot));
    }
}

public sealed class VertexBufferLayout(
    int slot,
    int stride,
    VertexInputRate inputRate,
    params VertexElement[] elements)
{
    public int Slot { get; } = slot;
    public int Stride { get; } = stride;
    public VertexInputRate InputRate { get; } = inputRate;
    public ReadOnlySpan<VertexElement> Elements => elements;
}

public readonly record struct VertexElement
{
    public VertexElement(string name, VertexAttributeFormat format, int offset)
        : this(new ShaderAttributeBinding(name, ShaderValueType.Unknown), format, offset)
    {
    }

    public VertexElement(ShaderAttributeBinding attribute, VertexAttributeFormat format, int offset)
    {
        Attribute = attribute;
        Format = format;
        Offset = offset;
    }

    public ShaderAttributeBinding Attribute { get; }
    public string Name => Attribute.Name;
    public VertexAttributeFormat Format { get; }
    public int Offset { get; }
}

public enum VertexInputRate
{
    Vertex,
    Instance
}

public readonly record struct DrawCommand(
    int VertexCount,
    int FirstVertex = 0);

public readonly record struct DrawInstancedCommand(
    int VertexCount,
    int InstanceCount,
    int FirstVertex = 0);

[StructLayout(LayoutKind.Sequential)]
public readonly record struct IndirectDrawCommand(
    uint VertexCount,
    uint InstanceCount,
    uint FirstVertex = 0,
    uint FirstInstance = 0);

public readonly record struct DrawIndirectCommand(
    IGraphicsBuffer Buffer,
    int Offset,
    int DrawCount);
