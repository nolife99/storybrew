namespace BrewLib.Graphics.Backend.SDL;

using System;
using BrewLib.Graphics.Shaders;

public sealed class SdlShaderProgramFactory : IShaderProgramFactory
{
    public IShaderProgramBackend CreateProgram(ShaderProgramSource source)
        => throw new NotSupportedException(
            "The SDL backend does not support legacy Shader programs. Use RenderPipeline with ShaderCross-ready HLSL sources.");
}
