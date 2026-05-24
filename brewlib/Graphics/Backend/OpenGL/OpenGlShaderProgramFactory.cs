namespace BrewLib.Graphics.Backend.OpenGL;

using Shaders;

public sealed class OpenGlShaderProgramFactory(OpenGlGraphicsBackend backend, OpenGlGraphicsDevice device) : IShaderProgramFactory
{
    public IShaderProgramBackend CreateProgram(ShaderProgramSource source)
        => new OpenGlShaderProgram(backend, device, source);
}