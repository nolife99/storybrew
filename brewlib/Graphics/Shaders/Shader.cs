namespace BrewLib.Graphics.Shaders;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OpenTK.Graphics.OpenGL;
using Util;

public sealed partial class Shader : IDisposable
{
    readonly StringBuilder log = new();

    Dictionary<string, Property<ActiveAttribType>> attributes;

    bool isInitialized, started;
    Dictionary<string, Property<ActiveUniformType>> uniforms;
    int vertexShaderId = -1, fragmentShaderId = -1, SortId = -1;

    public Shader(StringBuilder vertexShaderCode, StringBuilder fragmentShaderCode)
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

    public int GetAttributeLocation(string name) => attributes.TryGetValue(name, out var property) ? property.Location : -1;

    public int GetUniformLocation(string name, int index = -1, string field = null)
    {
        var location = uniforms.TryGetValue(GetUniformIdentifier(name, index, field), out var property) ?
            property.Location :
            -1;

        if (location < 0) throw new ArgumentException($"{name} isn't a valid uniform identifier");

        return location;
    }

    static string GetUniformIdentifier(string name, int index, string field)
        => name + (index >= 0 ? $"[{index}]" : "") + (field is not null ? "." + field : "");

    void initialize(StringBuilder vertexShaderCode, StringBuilder fragmentShaderCode)
    {
        dispose();
        GL.EnableVertexAttribArray(0);

        vertexShaderId = compileShader(OpenTK.Graphics.OpenGL.ShaderType.VertexShader, vertexShaderCode.ToString());
        fragmentShaderId = compileShader(OpenTK.Graphics.OpenGL.ShaderType.FragmentShader, fragmentShaderCode.ToString());

        if (vertexShaderId == -1 || fragmentShaderId == -1) return;

        SortId = linkProgram();
        isInitialized = SortId != -1;
    }

    int compileShader(OpenTK.Graphics.OpenGL.ShaderType type, string code)
    {
        var id = GL.CreateShader(type);
        GL.ShaderSource(id, code);
        GL.CompileShader(id);
        GL.GetShader(id, ShaderParameter.CompileStatus, out var compileStatus);

        if (compileStatus != 0) return id;

        log.AppendLine(CultureInfo.InvariantCulture, $"--- {type} ---\n{addLineExtracts(GL.GetShaderInfoLog(id), code)}");
        return -1;
    }

    int linkProgram()
    {
        var id = GL.CreateProgram();
        GL.AttachShader(id, vertexShaderId);
        GL.AttachShader(id, fragmentShaderId);
        GL.LinkProgram(id);
        GL.GetProgram(id, GetProgramParameterName.LinkStatus, out var linkStatus);

        if (linkStatus != 0) return id;

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
        if (vertexShaderId != -1) GL.DeleteShader(vertexShaderId);
        if (fragmentShaderId != -1) GL.DeleteShader(fragmentShaderId);
    }

    static string addLineExtracts(string log, string code)
    {
        var errorRegex = ErrRegex();
        var splitCode = code.Replace("\r\n", "\n").Split('\n');

        var sb = StringHelper.StringBuilderPool.Retrieve();
        foreach (var line in log.Split('\n'))
        {
            sb.AppendLine(line);

            var match = errorRegex.Match(line);
            if (!match.Success) continue;

            var character = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var lineNumber = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) - 1;

            if (lineNumber > 0)
            {
                sb.Append("  ");
                sb.AppendLine(splitCode[lineNumber - 1]);
            }

            sb.Append("> ");
            sb.AppendLine(splitCode[lineNumber]);
            sb.Append(new string(' ', character + 2));
            sb.AppendLine("^");
        }

        var sbCode = sb.ToString();
        StringHelper.StringBuilderPool.Release(sb);
        return sbCode;
    }

    public override string ToString() => $"program:{SortId} vs:{vertexShaderId} fs:{fragmentShaderId}";

    [GeneratedRegex(@"^ERROR: (\d+):(\d+): ", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ErrRegex();

    readonly record struct Property<TType>(int Size, TType Type, int Location);
}