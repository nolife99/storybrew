namespace BrewLib.Graphics.Shaders;

public enum CompiledShaderFormat
{
    Unknown,
    Glsl,
    SpirV,
    Dxil,
    Msl,
    MetalLib
}

public enum CompiledShaderStage
{
    Vertex,
    Fragment,
    Compute
}

public readonly record struct ShaderAsset(
    string Name,
    CompiledShaderStage Stage,
    CompiledShaderFormat Format,
    byte[] Code,
    string EntryPoint = "main");