namespace BrewLib.Graphics.Shaders;

using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public sealed partial class Shader : IDisposable
{
    readonly StringBuilder log = new();

    PooledDictionary<string, Property<ActiveAttribType>> attributes;

    bool isInitialized, started;
    int SortId = -1;
    PooledDictionary<string, Property<ActiveUniformType>> uniforms;

    public Shader(string vertexShaderCode, string fragmentShaderCode)
    {
        initialize(vertexShaderCode, fragmentShaderCode);
        if (!isInitialized)
        {
            dispose();
            throw new InvalidOperationException($"Failed to initialize shader:\n\n{log}");
        }

        retrieveAttributes();
        retrieveUniforms();
    }

    public void Dispose()
    {
        dispose();

        attributes.Dispose();
        uniforms.Dispose();

        GC.SuppressFinalize(this);
    }

    public void Begin()
    {
        if (started) throw new InvalidOperationException("Already started");

        DrawState.ProgramId = SortId;
        started = true;
    }

    public void End()
    {
        if (!started) throw new InvalidOperationException("Not started");

        started = false;
    }

    public int GetAttributeLocation(scoped ReadOnlySpan<char> name)
        => attributes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out var property) ?
            property.Location :
            -1;

    public int GetUniformLocation(scoped ReadOnlySpan<char> name, int index = -1, string field = null)
    {
        Span<char> buffer = stackalloc char[256];
        buffer = buffer[..(GetUniformIdentifier(buffer, name, index, field) - 1)];

        var location = uniforms.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(buffer, out var property) ?
            property.Location :
            -1;

        if (location < 0) throw new ArgumentException($"{name} isn't a valid uniform identifier ({buffer})");

        return location;
    }

    static int GetUniformIdentifier(scoped Span<char> buffer, scoped ReadOnlySpan<char> name, int index, string field)
    {
        var total = 0;

        name.CopyTo(buffer);
        buffer = buffer[name.Length..];

        if (index >= 0)
        {
            buffer[0] = '[';
            buffer = buffer[1..];

            index.TryFormat(buffer, out var charsWritten, provider: CultureInfo.InvariantCulture);
            buffer[charsWritten] = ']';

            buffer = buffer[1..];
            total += charsWritten + 2;
        }

        if (field is not null)
        {
            buffer[0] = '.';
            field.CopyTo(buffer[1..]);

            total += field.Length + 1;
        }

        return total + name.Length + 1;
    }

    void initialize(string vertexShaderCode, string fragmentShaderCode)
    {
        dispose();

        var vertexShaderId = compileShader(OpenTK.Graphics.OpenGL.ShaderType.VertexShader, vertexShaderCode);
        var fragmentShaderId = compileShader(OpenTK.Graphics.OpenGL.ShaderType.FragmentShader, fragmentShaderCode);

        if (vertexShaderId == -1 || fragmentShaderId == -1) return;

        SortId = linkProgram(vertexShaderId, fragmentShaderId);
        isInitialized = SortId != -1;
    }

    int compileShader(OpenTK.Graphics.OpenGL.ShaderType type, string code)
    {
        if (DrawState.Extensions.Contains("GL_KHR_parallel_shader_compile"))
            GL.Khr.MaxShaderCompilerThreads(GL.GetInteger((GetPName)All.MaxShaderCompilerThreadsKhr));

        var id = GL.CreateShader(type);
        GL.ShaderSource(id, code);
        GL.CompileShader(id);
        GL.GetShader(id, ShaderParameter.CompileStatus, out var compileStatus);

        if (compileStatus != 0) return id;

        log.AppendLine(CultureInfo.InvariantCulture,
            $"--- {type} ---\n{addLineExtracts(GL.GetShaderInfoLog(id), code)}");

        return -1;
    }

    int linkProgram(params ReadOnlySpan<int> shaders)
    {
        var id = GL.CreateProgram();
        foreach (var shader in shaders) GL.AttachShader(id, shader);
        GL.LinkProgram(id);
        foreach (var shader in shaders) GL.DetachShader(id, shader);

        GL.GetProgram(id, GetProgramParameterName.LinkStatus, out var linkStatus);
        if (linkStatus != 0)
        {
            foreach (var shader in shaders) GL.DeleteShader(shader);
            return id;
        }

        log.AppendLine(GL.GetProgramInfoLog(id));
        return -1;
    }

    void retrieveAttributes()
    {
        GL.GetProgram(SortId, GetProgramParameterName.ActiveAttributes, out var attributeCount);

        attributes = new(attributeCount);
        for (var i = 0; i < attributeCount; ++i)
        {
            var name = GL.GetActiveAttrib(SortId, i, out var size, out var type);
            attributes[name] = new(size, type, GL.GetAttribLocation(SortId, name));
        }
    }

    void retrieveUniforms()
    {
        GL.GetProgram(SortId, GetProgramParameterName.ActiveUniforms, out var uniformCount);

        uniforms = new(uniformCount);
        for (var i = 0; i < uniformCount; ++i)
        {
            var name = GL.GetActiveUniform(SortId, i, out var size, out var type);
            uniforms[name] = new(size, type, GL.GetUniformLocation(SortId, name));
        }
    }

    ~Shader() => dispose();

    void dispose()
    {
        if (!isInitialized) return;

        isInitialized = false;

        if (started) End();

        if (SortId != -1) GL.DeleteProgram(SortId);
    }

    static string addLineExtracts(string log, string code)
    {
        var errorRegex = ErrRegex();

        var toSplit = code.Replace("\r\n", "\n").AsSpan();
        using var splitCode = toSplit.Split(['\n']);

        using var sb = TempList.Create<char>();

        var logSpan = log.AsSpan();
        foreach (var line in logSpan.Split('\n'))
        {
            var splitLine = logSpan[line];
            sb.AddRange(splitLine);

            if (!errorRegex.IsMatch(splitLine)) continue;

            var match = errorRegex.Match(splitLine.ToString());

            if (int.TryParse(match.Groups[2].ValueSpan, CultureInfo.InvariantCulture, out var lineNumber)) --lineNumber;

            if (lineNumber > 0) sb.Append($"  {toSplit[splitCode[lineNumber - 1]]}\n");
            sb.Append($"> {toSplit[splitCode[lineNumber]]}");

            if (int.TryParse(match.Groups[1].ValueSpan, CultureInfo.InvariantCulture, out var character))
                for (var i = 0; i < character + 2; ++i)
                    sb.Add(' ');

            sb.AddRange("^\n");
        }

        return sb.AsReadOnlySpan().ToString();
    }

    [GeneratedRegex(@"^ERROR: (\d+):(\d+): ", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ErrRegex();

    readonly record struct Property<TType>(int Size, TType Type, int Location);
}