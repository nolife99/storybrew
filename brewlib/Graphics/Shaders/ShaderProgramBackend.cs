namespace BrewLib.Graphics.Shaders;

using System;
using Backend;

public interface IShaderProgramFactory
{
    IShaderProgramBackend CreateProgram(ShaderProgramSource source);
}

public interface IShaderProgramBackend : IDisposable
{
    GraphicsResourceHandle NativeHandle { get; }

    ShaderAttributeInfo GetAttribute(scoped ReadOnlySpan<char> name);
    ShaderUniformInfo GetUniform(scoped ReadOnlySpan<char> name);

    void Bind();
    void Unbind();
    void SetUniform<T>(ShaderUniform<T> uniform, T value);
}

public readonly record struct ShaderAttributeInfo(
    string Name,
    ShaderValueType Type,
    int Size,
    int Location)
{
    public bool IsActive => Location >= 0;

    public static ShaderAttributeInfo Missing(scoped ReadOnlySpan<char> name)
        => new(name.ToString(), ShaderValueType.Unknown, 0, -1);
}

public readonly record struct ShaderUniformInfo(
    string Name,
    ShaderValueType Type,
    int Size,
    int Location)
{
    public bool IsActive => Location >= 0;

    public static ShaderUniformInfo Missing(scoped ReadOnlySpan<char> name)
        => new(name.ToString(), ShaderValueType.Unknown, 0, -1);
}
