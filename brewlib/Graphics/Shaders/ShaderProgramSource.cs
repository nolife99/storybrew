namespace BrewLib.Graphics.Shaders;

public enum ShaderSourceLanguage
{
    Glsl,
    Hlsl
}

public readonly record struct ShaderProgramSource(
    string Name,
    string VertexSource,
    string FragmentSource,
    ShaderSourceLanguage Language = ShaderSourceLanguage.Glsl,
    string VertexEntryPoint = "main",
    string FragmentEntryPoint = "main");
