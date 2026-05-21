namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;

public enum ShaderSourceLanguage
{
    Glsl,
    Hlsl,
    Wgsl
}

public readonly record struct ShaderProgramSource(
    string Name,
    string VertexSource,
    string FragmentSource,
    ShaderSourceLanguage Language = ShaderSourceLanguage.Hlsl,
       string VertexEntryPoint = "main",
       string FragmentEntryPoint = "main",
       IReadOnlyList<string> VertexInputNames = null,
       IReadOnlyList<string> FragmentTextureNames = null,
       IReadOnlyList<ShaderUniformBlockBinding> UniformBlocks = null);
