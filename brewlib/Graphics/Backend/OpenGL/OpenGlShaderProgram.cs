namespace BrewLib.Graphics.Backend.OpenGL;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Shaders;
using Silk.NET.OpenGL;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Util;
using GlShaderType = Silk.NET.OpenGL.ShaderType;

internal sealed partial class OpenGlShaderProgram : IShaderProgramBackend
{
    readonly OpenGlGraphicsBackend backend;
    readonly OpenGlGraphicsDevice device;
    readonly StringBuilder log = new();
    readonly PooledDictionary<string, ShaderAttributeInfo> attributes = new();
    readonly PooledDictionary<string, ShaderUniformInfo> uniforms = new();

    bool initialized;
    int programId = -1;

    public OpenGlShaderProgram(OpenGlGraphicsBackend backend, OpenGlGraphicsDevice device, ShaderProgramSource source)
    {
        this.backend = backend;
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
        var location = OpenGlApi.GL.GetUniformLocation((uint)programId, identifier);
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
                OpenGlApi.GL.Uniform1(location, i);
                break;

            case bool b:
                OpenGlApi.GL.Uniform1(location, b ? 1 : 0);
                break;

            case float f:
                OpenGlApi.GL.Uniform1(location, f);
                break;

            case Vector2 v:
                OpenGlApi.GL.Uniform2(location, v.X, v.Y);
                break;

            case Vector3 v:
                OpenGlApi.GL.Uniform3(location, v.X, v.Y, v.Z);
                break;

            case Vector4 v:
                OpenGlApi.GL.Uniform4(location, v.X, v.Y, v.Z, v.W);
                break;

            case Matrix4x4 m:
                OpenGlApi.GL.UniformMatrix4(location, 1, false, ref m.M11);
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

        source = OpenGlShaderCompiler.CreateShaderSource(source,
            backend.GlslVersion,
            backend.GlslEs);
        var vertexShaderId = compileShader(GlShaderType.VertexShader, source.VertexSource);
        var fragmentShaderId = compileShader(GlShaderType.FragmentShader, source.FragmentSource);

        if (vertexShaderId == -1 || fragmentShaderId == -1)
        {
            if (vertexShaderId != -1) OpenGlApi.GL.DeleteShader((uint)vertexShaderId);
            if (fragmentShaderId != -1) OpenGlApi.GL.DeleteShader((uint)fragmentShaderId);
            return;
        }

        programId = linkProgram(vertexShaderId, fragmentShaderId);
        initialized = programId != -1;
        if (initialized) bindUniformBlocks(source.UniformBlocks);
    }

    int compileShader(GlShaderType type, string code)
    {
        var id = (int)OpenGlApi.GL.CreateShader(type);
        OpenGlApi.GL.ShaderSource((uint)id, code);
        OpenGlApi.GL.CompileShader((uint)id);
        OpenGlApi.GL.GetShader((uint)id, ShaderParameterName.CompileStatus, out var compileStatus);

        if (compileStatus != 0) return id;

        log.AppendLine(CultureInfo.InvariantCulture,
            $"--- {type} ---\n{addLineExtracts(OpenGlApi.GL.GetShaderInfoLog((uint)id), code)}");

        OpenGlApi.GL.DeleteShader((uint)id);
        return -1;
    }

    int linkProgram(params ReadOnlySpan<int> shaders)
    {
        var id = (int)OpenGlApi.GL.CreateProgram();
        foreach (var shader in shaders) OpenGlApi.GL.AttachShader((uint)id, (uint)shader);
        OpenGlApi.GL.LinkProgram((uint)id);
        foreach (var shader in shaders) OpenGlApi.GL.DetachShader((uint)id, (uint)shader);

        OpenGlApi.GL.GetProgram((uint)id, ProgramPropertyARB.LinkStatus, out var linkStatus);
        foreach (var shader in shaders) OpenGlApi.GL.DeleteShader((uint)shader);

        if (linkStatus != 0) return id;

        log.AppendLine(OpenGlApi.GL.GetProgramInfoLog((uint)id));
        OpenGlApi.GL.DeleteProgram((uint)id);
        return -1;
    }

    void bindUniformBlocks(IReadOnlyList<ShaderUniformBlockBinding> uniformBlocks)
    {
        if (uniformBlocks is null) return;

        for (var i = 0; i < uniformBlocks.Count; ++i)
        {
            var uniformBlock = uniformBlocks[i];
            var index = OpenGlApi.GL.GetUniformBlockIndex((uint)programId, uniformBlock.Name);
            if (index == uint.MaxValue) continue;

            OpenGlApi.GL.UniformBlockBinding((uint)programId, index, uniformBlock.Slot);
        }
    }

    void retrieveAttributes()
    {
        OpenGlApi.GL.GetProgram((uint)programId, ProgramPropertyARB.ActiveAttributes, out var attributeCount);

        for (var i = 0; i < attributeCount; ++i)
        {
            var name = OpenGlApi.GL.GetActiveAttrib((uint)programId, (uint)i, out var size, out var type);
            attributes[name] = new(name,
                toShaderValueType(type),
                size,
                OpenGlApi.GL.GetAttribLocation((uint)programId, name));
        }
    }

    void retrieveUniforms()
    {
        OpenGlApi.GL.GetProgram((uint)programId, ProgramPropertyARB.ActiveUniforms, out var uniformCount);

        for (var i = 0; i < uniformCount; ++i)
        {
            var name = OpenGlApi.GL.GetActiveUniform((uint)programId, (uint)i, out var size, out var type);
            uniforms[name] = new(name,
                toShaderValueType(type),
                size,
                OpenGlApi.GL.GetUniformLocation((uint)programId, name));
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
        if (programId != -1) OpenGlApi.GL.DeleteProgram((uint)programId);
        programId = -1;
    }

    static ShaderValueType toShaderValueType(AttributeType type)
        => type switch
        {
            AttributeType.Float => ShaderValueType.Float,
            AttributeType.FloatVec2 => ShaderValueType.FloatVec2,
            AttributeType.FloatVec3 => ShaderValueType.FloatVec3,
            AttributeType.FloatVec4 => ShaderValueType.FloatVec4,
            AttributeType.Int => ShaderValueType.Int,
            AttributeType.IntVec2 => ShaderValueType.IntVec2,
            AttributeType.IntVec3 => ShaderValueType.IntVec3,
            AttributeType.IntVec4 => ShaderValueType.IntVec4,
            AttributeType.UnsignedInt => ShaderValueType.UnsignedInt,
            AttributeType.UnsignedIntVec2 => ShaderValueType.UnsignedIntVec2,
            AttributeType.UnsignedIntVec3 => ShaderValueType.UnsignedIntVec3,
            AttributeType.UnsignedIntVec4 => ShaderValueType.UnsignedIntVec4,
            _ => ShaderValueType.Unknown
        };

    static ShaderValueType toShaderValueType(UniformType type)
        => type switch
        {
            UniformType.Int => ShaderValueType.Int,
            UniformType.UnsignedInt => ShaderValueType.UnsignedInt,
            UniformType.Float => ShaderValueType.Float,
            UniformType.Double => ShaderValueType.Double,
            UniformType.FloatVec2 => ShaderValueType.FloatVec2,
            UniformType.FloatVec3 => ShaderValueType.FloatVec3,
            UniformType.FloatVec4 => ShaderValueType.FloatVec4,
            UniformType.IntVec2 => ShaderValueType.IntVec2,
            UniformType.IntVec3 => ShaderValueType.IntVec3,
            UniformType.IntVec4 => ShaderValueType.IntVec4,
            UniformType.UnsignedIntVec2 => ShaderValueType.UnsignedIntVec2,
            UniformType.UnsignedIntVec3 => ShaderValueType.UnsignedIntVec3,
            UniformType.UnsignedIntVec4 => ShaderValueType.UnsignedIntVec4,
            UniformType.Bool => ShaderValueType.Bool,
            UniformType.BoolVec2 => ShaderValueType.BoolVec2,
            UniformType.BoolVec3 => ShaderValueType.BoolVec3,
            UniformType.BoolVec4 => ShaderValueType.BoolVec4,
            UniformType.FloatMat2 => ShaderValueType.FloatMat2,
            UniformType.FloatMat3 => ShaderValueType.FloatMat3,
            UniformType.FloatMat4 => ShaderValueType.FloatMat4,
            UniformType.Sampler1D => ShaderValueType.Sampler1D,
            UniformType.Sampler2D => ShaderValueType.Sampler2D,
            UniformType.Sampler3D => ShaderValueType.Sampler3D,
            UniformType.SamplerCube => ShaderValueType.SamplerCube,
            UniformType.Sampler1DArray => ShaderValueType.Sampler1DArray,
            UniformType.Sampler2DArray => ShaderValueType.Sampler2DArray,
            UniformType.SamplerCubeMapArray => ShaderValueType.SamplerCubeArray,
            UniformType.SamplerBuffer => ShaderValueType.SamplerBuffer,
            UniformType.IntSampler1D => ShaderValueType.IntSampler1D,
            UniformType.IntSampler2D => ShaderValueType.IntSampler2D,
            UniformType.IntSampler3D => ShaderValueType.IntSampler3D,
            UniformType.IntSamplerCube => ShaderValueType.IntSamplerCube,
            UniformType.IntSampler1DArray => ShaderValueType.IntSampler1DArray,
            UniformType.IntSampler2DArray => ShaderValueType.IntSampler2DArray,
            UniformType.IntSamplerBuffer => ShaderValueType.IntSamplerBuffer,
            UniformType.UnsignedIntSampler1D => ShaderValueType.UnsignedIntSampler1D,
            UniformType.UnsignedIntSampler2D => ShaderValueType.UnsignedIntSampler2D,
            UniformType.UnsignedIntSampler3D => ShaderValueType.UnsignedIntSampler3D,
            UniformType.UnsignedIntSamplerCube => ShaderValueType.UnsignedIntSamplerCube,
            UniformType.UnsignedIntSampler1DArray => ShaderValueType.UnsignedIntSampler1DArray,
            UniformType.UnsignedIntSampler2DArray => ShaderValueType.UnsignedIntSampler2DArray,
            UniformType.UnsignedIntSamplerBuffer => ShaderValueType.UnsignedIntSamplerBuffer,
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
