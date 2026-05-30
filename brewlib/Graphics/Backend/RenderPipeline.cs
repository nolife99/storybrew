namespace BrewLib.Graphics.Backend;

using System;
using Renderers;
using Shaders;
using Textures;

public interface IRenderPipeline : IDisposable
{
    RenderPipelineDescription Description { get; }

    IRenderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name) where T : struct;
    IRenderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform) where T : struct;
    IResourceSet CreateResourceSet();

    void Draw(DrawCommand command, scoped ReadOnlySpan<RenderVertexBufferBinding> vertexBuffers, IResourceSet resources = null);
    void DrawInstanced(DrawInstancedCommand command, scoped ReadOnlySpan<RenderVertexBufferBinding> vertexBuffers, IResourceSet resources = null);
}

public interface IRenderPipelineFactory
{
    IRenderPipeline CreateRenderPipeline(RenderPipelineDescription description);
}

public interface IRenderUniform<in T> where T : struct
{
    void SetValue(T value);
}

public interface IResourceSet : IDisposable
{
    void SetTextures(ShaderSamplerBinding slot, scoped ReadOnlySpan<ITexture> textures);
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

    public TextureBindingLayout GetTextureBinding(ShaderSamplerBinding slot)
    {
        foreach (var textureBinding in textureBindings)
            if (textureBinding.Slot.Name == slot.Name)
                return textureBinding;

        throw new ArgumentException($"Texture binding '{slot.Name}' is not part of this pipeline layout", nameof(slot));
    }
}

public readonly record struct TextureBindingLayout
{
    public TextureBindingLayout(string name, int capacity, bool arrayed = false)
        : this(new ShaderSamplerBinding(name), capacity, arrayed) { }

    public TextureBindingLayout(ShaderSamplerBinding slot, int capacity, bool arrayed = false)
    {
        Slot = slot;
        Capacity = capacity;
        Arrayed = arrayed;
    }

    public ShaderSamplerBinding Slot { get; }
    public string Name => Slot.Name;
    public int Capacity { get; }

    /// <summary>
    /// When true the binding is a single <c>binding_array&lt;texture_2d, Capacity&gt;</c> at the
    /// slot's base binding plus one shared sampler at base+1 (the bindless/non-uniform path).
    /// When false the binding expands to <c>Capacity</c> discrete (texture, sampler) pairs
    /// occupying bindings <c>0..2*Capacity</c> (the waterfall path).
    /// </summary>
    public bool Arrayed { get; }
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
        : this(new ShaderAttributeBinding(name, ShaderValueType.Unknown), format, offset) { }

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

public readonly record struct RenderVertexBufferBinding(
    int Slot,
    IGraphicsBuffer Buffer,
    int Offset = -1);

public readonly record struct DrawCommand(
    int VertexCount,
    int FirstVertex = 0);

public readonly record struct DrawInstancedCommand(
    int VertexCount,
    int InstanceCount,
    int FirstVertex = 0);
