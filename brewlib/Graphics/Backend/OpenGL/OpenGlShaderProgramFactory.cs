namespace BrewLib.Graphics.Backend.OpenGL;

using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Shaders;

public sealed class OpenGlShaderProgramFactory(OpenGlGraphicsDevice device) : IShaderProgramFactory
{
    public IShaderProgramBackend CreateProgram(ShaderProgramSource source)
        => new OpenGlShaderProgram(device, source);
}
