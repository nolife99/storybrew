namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using BrewLib.Graphics.Backend;
using BrewLib.Graphics.Shaders;
using BrewLib.Util;
using osuTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

internal sealed partial class OpenGlShaderProgram : IShaderProgramBackend
{
    readonly OpenGlGraphicsDevice device;
    readonly StringBuilder log = new();
    readonly PooledDictionary<string, ShaderAttributeInfo> attributes = new();
    readonly PooledDictionary<string, ShaderUniformInfo> uniforms = new();

    bool initialized;
    int programId = -1;

    public OpenGlShaderProgram(OpenGlGraphicsDevice device, ShaderProgramSource source)
    {
        this.device = device;

        initialize(source);
        if (!initialized)
        {
            dispose();
            throw new InvalidOperationException($"Failed to initialize shader {source.Name}:\n\n{log}");
        }

        retrieveAttributes();
        retrieveUniforms();
    }

    public GraphicsResourceHandle NativeHandle => new("OpenGL", programId);

    public ShaderAttributeInfo GetAttribute(scoped ReadOnlySpan<char> name)
        => attributes.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out var attribute) ?
            attribute :
            ShaderAttributeInfo.Missing(name);

    public ShaderUniformInfo GetUniform(scoped ReadOnlySpan<char> name)
    {
        if (uniforms.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out var uniform))
            return uniform;

        var identifier = name.ToString();
        var location = GL.GetUniformLocation(programId, identifier);
        if (location < 0) return ShaderUniformInfo.Missing(name);

        var type = getArrayBaseType(identifier);
        uniform = new(identifier, type, 1, location);
        uniforms[identifier] = uniform;
        return uniform;
    }

    public void Bind() => device.UseProgram(programId);

    public void Unbind()
    {
    }

    public void SetUniform<T>(ShaderUniform<T> uniform, T value)
    {
        var location = uniform.Location;
        if (location < 0) return;

        switch (value)
        {
            case int i:
                GL.Uniform1(location, i);
                break;

            case bool b:
                GL.Uniform1(location, b ? 1 : 0);
                break;

            case float f:
                GL.Uniform1(location, f);
                break;

            case Vector2 v:
                GL.Uniform2(location, v.X, v.Y);
                break;

            case Vector3 v:
                GL.Uniform3(location, v.X, v.Y, v.Z);
                break;

            case Vector4 v:
                GL.Uniform4(location, v.X, v.Y, v.Z, v.W);
                break;

            case Matrix4x4 m:
                GL.UniformMatrix4(location, 1, false, ref m.M11);
                break;

            default:
                throw new NotSupportedException($"Uniform type {typeof(T).Name} is not supported by the OpenGL backend");
        }
    }

    public void Dispose()
    {
        dispose();

        attributes.Dispose();
        uniforms.Dispose();

        GC.SuppressFinalize(this);
    }

    void initialize(ShaderProgramSource source)
    {
        dispose();

        var vertexShaderId = compileShader(osuTK.Graphics.OpenGL.ShaderType.VertexShader, source.VertexSource);
        var fragmentShaderId = compileShader(osuTK.Graphics.OpenGL.ShaderType.FragmentShader, source.FragmentSource);

        if (vertexShaderId == -1 || fragmentShaderId == -1)
        {
            if (vertexShaderId != -1) GL.DeleteShader(vertexShaderId);
            if (fragmentShaderId != -1) GL.DeleteShader(fragmentShaderId);
            return;
        }

        programId = linkProgram(vertexShaderId, fragmentShaderId);
        initialized = programId != -1;
    }

    int compileShader(osuTK.Graphics.OpenGL.ShaderType type, string code)
    {
        var id = GL.CreateShader(type);
        GL.ShaderSource(id, code);
        GL.CompileShader(id);
        GL.GetShader(id, ShaderParameter.CompileStatus, out var compileStatus);

        if (compileStatus != 0) return id;

        log.AppendLine(CultureInfo.InvariantCulture,
            $"--- {type} ---\n{addLineExtracts(GL.GetShaderInfoLog(id), code)}");

        GL.DeleteShader(id);
        return -1;
    }

    int linkProgram(params ReadOnlySpan<int> shaders)
    {
        var id = GL.CreateProgram();
        foreach (var shader in shaders) GL.AttachShader(id, shader);
        GL.LinkProgram(id);
        foreach (var shader in shaders) GL.DetachShader(id, shader);

        GL.GetProgram(id, GetProgramParameterName.LinkStatus, out var linkStatus);
        foreach (var shader in shaders) GL.DeleteShader(shader);

        if (linkStatus != 0) return id;

        log.AppendLine(GL.GetProgramInfoLog(id));
        GL.DeleteProgram(id);
        return -1;
    }

    void retrieveAttributes()
    {
        GL.GetProgram(programId, GetProgramParameterName.ActiveAttributes, out var attributeCount);

        for (var i = 0; i < attributeCount; ++i)
        {
            var name = GL.GetActiveAttrib(programId, i, out var size, out var type);
            attributes[name] = new(name, toShaderValueType(type), size, GL.GetAttribLocation(programId, name));
        }
    }

    void retrieveUniforms()
    {
        GL.GetProgram(programId, GetProgramParameterName.ActiveUniforms, out var uniformCount);

        for (var i = 0; i < uniformCount; ++i)
        {
            var name = GL.GetActiveUniform(programId, i, out var size, out var type);
            uniforms[name] = new(name, toShaderValueType(type), size, GL.GetUniformLocation(programId, name));
        }
    }

    ShaderValueType getArrayBaseType(string identifier)
    {
        var bracketIndex = identifier.IndexOf('[', StringComparison.Ordinal);
        if (bracketIndex <= 0) return ShaderValueType.Unknown;

        var baseIdentifier = string.Concat(identifier.AsSpan(0, bracketIndex), "[0]");
        return uniforms.TryGetValue(baseIdentifier, out var uniform) ? uniform.Type : ShaderValueType.Unknown;
    }

    ~OpenGlShaderProgram() => dispose();

    void dispose()
    {
        if (!initialized) return;

        initialized = false;
        if (programId != -1) GL.DeleteProgram(programId);
        programId = -1;
    }

    static ShaderValueType toShaderValueType(ActiveAttribType type)
        => type switch
        {
            ActiveAttribType.Float => ShaderValueType.Float,
            ActiveAttribType.FloatVec2 => ShaderValueType.FloatVec2,
            ActiveAttribType.FloatVec3 => ShaderValueType.FloatVec3,
            ActiveAttribType.FloatVec4 => ShaderValueType.FloatVec4,
            ActiveAttribType.Int => ShaderValueType.Int,
            ActiveAttribType.IntVec2 => ShaderValueType.IntVec2,
            ActiveAttribType.IntVec3 => ShaderValueType.IntVec3,
            ActiveAttribType.IntVec4 => ShaderValueType.IntVec4,
            ActiveAttribType.UnsignedInt => ShaderValueType.UnsignedInt,
            ActiveAttribType.UnsignedIntVec2 => ShaderValueType.UnsignedIntVec2,
            ActiveAttribType.UnsignedIntVec3 => ShaderValueType.UnsignedIntVec3,
            ActiveAttribType.UnsignedIntVec4 => ShaderValueType.UnsignedIntVec4,
            _ => ShaderValueType.Unknown
        };

    static ShaderValueType toShaderValueType(ActiveUniformType type)
        => type switch
        {
            ActiveUniformType.Int => ShaderValueType.Int,
            ActiveUniformType.UnsignedInt => ShaderValueType.UnsignedInt,
            ActiveUniformType.Float => ShaderValueType.Float,
            ActiveUniformType.Double => ShaderValueType.Double,
            ActiveUniformType.FloatVec2 => ShaderValueType.FloatVec2,
            ActiveUniformType.FloatVec3 => ShaderValueType.FloatVec3,
            ActiveUniformType.FloatVec4 => ShaderValueType.FloatVec4,
            ActiveUniformType.IntVec2 => ShaderValueType.IntVec2,
            ActiveUniformType.IntVec3 => ShaderValueType.IntVec3,
            ActiveUniformType.IntVec4 => ShaderValueType.IntVec4,
            ActiveUniformType.UnsignedIntVec2 => ShaderValueType.UnsignedIntVec2,
            ActiveUniformType.UnsignedIntVec3 => ShaderValueType.UnsignedIntVec3,
            ActiveUniformType.UnsignedIntVec4 => ShaderValueType.UnsignedIntVec4,
            ActiveUniformType.Bool => ShaderValueType.Bool,
            ActiveUniformType.BoolVec2 => ShaderValueType.BoolVec2,
            ActiveUniformType.BoolVec3 => ShaderValueType.BoolVec3,
            ActiveUniformType.BoolVec4 => ShaderValueType.BoolVec4,
            ActiveUniformType.FloatMat2 => ShaderValueType.FloatMat2,
            ActiveUniformType.FloatMat3 => ShaderValueType.FloatMat3,
            ActiveUniformType.FloatMat4 => ShaderValueType.FloatMat4,
            ActiveUniformType.Sampler1D => ShaderValueType.Sampler1D,
            ActiveUniformType.Sampler2D => ShaderValueType.Sampler2D,
            ActiveUniformType.Sampler3D => ShaderValueType.Sampler3D,
            ActiveUniformType.SamplerCube => ShaderValueType.SamplerCube,
            ActiveUniformType.Sampler1DArray => ShaderValueType.Sampler1DArray,
            ActiveUniformType.Sampler2DArray => ShaderValueType.Sampler2DArray,
            ActiveUniformType.SamplerCubeMapArray => ShaderValueType.SamplerCubeArray,
            ActiveUniformType.SamplerBuffer => ShaderValueType.SamplerBuffer,
            ActiveUniformType.IntSampler1D => ShaderValueType.IntSampler1D,
            ActiveUniformType.IntSampler2D => ShaderValueType.IntSampler2D,
            ActiveUniformType.IntSampler3D => ShaderValueType.IntSampler3D,
            ActiveUniformType.IntSamplerCube => ShaderValueType.IntSamplerCube,
            ActiveUniformType.IntSampler1DArray => ShaderValueType.IntSampler1DArray,
            ActiveUniformType.IntSampler2DArray => ShaderValueType.IntSampler2DArray,
            ActiveUniformType.IntSamplerBuffer => ShaderValueType.IntSamplerBuffer,
            ActiveUniformType.UnsignedIntSampler1D => ShaderValueType.UnsignedIntSampler1D,
            ActiveUniformType.UnsignedIntSampler2D => ShaderValueType.UnsignedIntSampler2D,
            ActiveUniformType.UnsignedIntSampler3D => ShaderValueType.UnsignedIntSampler3D,
            ActiveUniformType.UnsignedIntSamplerCube => ShaderValueType.UnsignedIntSamplerCube,
            ActiveUniformType.UnsignedIntSampler1DArray => ShaderValueType.UnsignedIntSampler1DArray,
            ActiveUniformType.UnsignedIntSampler2DArray => ShaderValueType.UnsignedIntSampler2DArray,
            ActiveUniformType.UnsignedIntSamplerBuffer => ShaderValueType.UnsignedIntSamplerBuffer,
            _ => ShaderValueType.Unknown
        };

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
}
