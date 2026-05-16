namespace BrewLib.Graphics.Shaders;

public readonly record struct ShaderUniformBinding<T>(
    string Name,
    ShaderValueType Type,
    ShaderBindingStage Stage = ShaderBindingStage.Vertex,
    uint Slot = 0);

public readonly record struct ShaderSamplerBinding(
    string Name,
    ShaderValueType Type = ShaderValueType.Sampler2D);

public readonly record struct ShaderAttributeBinding(
    string Name,
    ShaderValueType Type);

public enum ShaderBindingStage
{
    Vertex,
    Fragment,
    Compute
}
