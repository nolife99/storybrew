namespace BrewLib.Graphics.Shaders;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Backend;

public sealed class Shader : IDisposable
{
    readonly IShaderProgramBackend program;
    readonly Dictionary<string, ShaderUniform> uniforms = new(StringComparer.Ordinal);

    bool disposed, started;

    public Shader(string vertexShaderCode, string fragmentShaderCode, IGraphicsBackend backend = null)
        : this(new("generated", vertexShaderCode, fragmentShaderCode), backend) { }

    public Shader(ShaderProgramSource source, IGraphicsBackend backend = null)
    {
        var factory = backend?.ShaderPrograms ?? DrawState.Backend?.ShaderPrograms ??
            throw new InvalidOperationException("A graphics backend must be initialized before creating shaders");

        program = factory.CreateProgram(source);
    }

    public GraphicsResourceHandle NativeHandle => program.NativeHandle;

    public void Dispose()
    {
        if (disposed) return;

        if (started) End();

        program.Dispose();
        uniforms.Clear();

        disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Begin()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) throw new InvalidOperationException("Already started");

        program.Bind();
        started = true;
    }

    public void End()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!started) throw new InvalidOperationException("Not started");

        program.Unbind();
        started = false;
    }

    public int GetAttributeLocation(scoped ReadOnlySpan<char> name)
        => program.GetAttribute(name).Location;

    public int GetUniformLocation(scoped ReadOnlySpan<char> name, int index = -1, string field = null)
    {
        var identifier = GetUniformIdentifier(name, index, field);
        var uniform = program.GetUniform(identifier);

        return uniform.Location < 0 ?
            throw new ArgumentException($"{name} isn't a valid uniform identifier ({identifier})") :
            uniform.Location;
    }

    public int GetUniformLocation(ShaderSamplerBinding sampler, int index = -1, string field = null)
        => GetUniformLocation(sampler.Name, index, field);

    public ShaderUniform<T> GetUniform<T>(scoped ReadOnlySpan<char> name, int index = -1, string field = null)
    {
        var identifier = GetUniformIdentifier(name, index, field);
        if (uniforms.TryGetValue(identifier, out var cached))
        {
            if (cached is ShaderUniform<T> typed) return typed;

            throw new ArgumentException(
                $"Uniform {identifier} was already requested as {cached.GetType().GenericTypeArguments[0].Name}");
        }

        var info = program.GetUniform(identifier);
        if (info.Location < 0)
            throw new ArgumentException($"{name} isn't a valid uniform identifier ({identifier})");

        if (!info.Type.IsCompatibleWith<T>())
            throw new ArgumentException(
                $"Uniform {identifier} has shader type {info.Type}, which is not compatible with {typeof(T).Name}");

        var uniform = new ShaderUniform<T>(program, info);
        uniforms.Add(identifier, uniform);
        return uniform;
    }

    public ShaderUniform<T> GetUniform<T>(ShaderUniformBinding<T> uniform, int index = -1, string field = null)
        => GetUniform<T>(uniform.Name, index, field);

    public ShaderUniform<T> TryGetUniform<T>(scoped ReadOnlySpan<char> name, int index = -1, string field = null)
    {
        var identifier = GetUniformIdentifier(name, index, field);
        if (uniforms.TryGetValue(identifier, out var cached)) return cached as ShaderUniform<T>;

        var info = program.GetUniform(identifier);
        if (info.Location < 0 || !info.Type.IsCompatibleWith<T>()) return null;

        var uniform = new ShaderUniform<T>(program, info);
        uniforms.Add(identifier, uniform);
        return uniform;
    }

    static string GetUniformIdentifier(scoped ReadOnlySpan<char> name, int index, string field)
    {
        if (index < 0 && field is null) return name.ToString();

        StringBuilder builder = new(name.Length + (index >= 0 ? 8 : 0) + (field?.Length + 1 ?? 0));
        builder.Append(name);

        if (index >= 0) builder.Append(CultureInfo.InvariantCulture, $"[{index}]");
        if (field is not null)
        {
            builder.Append('.');
            builder.Append(field);
        }

        return builder.ToString();
    }
}