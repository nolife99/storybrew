namespace BrewLib.Graphics;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using osuTK;
using osuTK.Graphics.OpenGL;

public class Shader : IDisposable
{
    readonly StringBuilder log = new();

    Dictionary<string, Property<ActiveAttribType>> attributes;

    bool isInitialized, started;
    Dictionary<string, Property<ActiveUniformType>> uniforms;
    int vertexShaderId = -1, fragmentShaderId = -1;

    public Shader(StringBuilder vertexShaderCode, StringBuilder fragmentShaderCode)
    {
        initialize(vertexShaderCode, fragmentShaderCode);
        if (!isInitialized)
        {
            Dispose(true);
            throw new GraphicsException($"Failed to initialize shader:\n\n{log}");
        }

        retrieveAttributes();
        retrieveUniforms();
    }

    public int SortId { get; private set; } = -1;

    public void Dispose()
    {
        Dispose(true);
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

    public int GetAttributeLocation(string name)
    {
        if (attributes.TryGetValue(name, out var property)) return property.Location;
        return -1;
    }

    public int TryGetUniformLocation(string name, int index = -1, string field = null)
    {
        if (uniforms.TryGetValue(GetUniformIdentifier(name, index, field), out var property)) return property.Location;
        return -1;
    }

    public int GetUniformLocation(string name, int index = -1, string field = null)
    {
        var location = TryGetUniformLocation(name, index, field);

        if (location < 0) throw new ArgumentException($"{name} isn't a valid uniform identifier");
        return location;
    }

    public static string GetUniformIdentifier(string name, int index, string field)
        => name + (index >= 0 ? "[" + index + "]" : "") + (field is not null ? "." + field : "");

    void initialize(StringBuilder vertexShaderCode, StringBuilder fragmentShaderCode)
    {
        Dispose(true);

        vertexShaderId = compileShader(ShaderType.VertexShader, vertexShaderCode.ToString());
        fragmentShaderId = compileShader(ShaderType.FragmentShader, fragmentShaderCode.ToString());

        if (vertexShaderId == -1 || fragmentShaderId == -1) return;

        SortId = linkProgram();
        isInitialized = SortId != -1;
    }

    int compileShader(ShaderType type, string code)
    {
        var id = GL.CreateShader(type);
        GL.ShaderSource(id, code);
        GL.CompileShader(id);
        GL.GetShader(id, ShaderParameter.CompileStatus, out var compileStatus);

        if (compileStatus != 0) return id;
        log.AppendLine(CultureInfo.InvariantCulture,
            $"--- {type} ---\n{addLineExtracts(GL.GetShaderInfoLog(id), code)}");
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
            var location = GL.GetAttribLocation(SortId, name);
            attributes[name] = new(name, size, type, location);
        }
    }

    void retrieveUniforms()
    {
        GL.GetProgram(SortId, GetProgramParameterName.ActiveUniforms, out var uniformCount);

        uniforms = new(uniformCount);
        for (var i = 0; i < uniformCount; ++i)
        {
            var name = GL.GetActiveUniform(SortId, i, out var size, out var type);
            uniforms[name] = new(name, size, type, GL.GetUniformLocation(SortId, name));
        }
    }

    ~Shader() => Dispose(false);

    protected virtual void Dispose(bool disposing)
    {
        if (isInitialized)
            isInitialized = false;
        else
            return;

        if (started) End();

        if (SortId != -1) GL.DeleteProgram(SortId);
        if (vertexShaderId != -1) GL.DeleteShader(vertexShaderId);
        if (fragmentShaderId != -1) GL.DeleteShader(fragmentShaderId);

        if (!disposing) return;

        SortId = -1;
        vertexShaderId = -1;
        fragmentShaderId = -1;

        attributes.Clear();
        attributes = null;

        uniforms.Clear();
        uniforms = null;
    }

    static string addLineExtracts(string log, string code)
    {
        Regex errorRegex = new(@"^ERROR: (\d+):(\d+): ", RegexOptions.IgnoreCase);
        var splitCode = code.Replace("\r\n", "\n").Split('\n');

        StringBuilder sb = new();
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
            sb.AppendLine(new string(' ', character + 2) + "^");
        }

        return sb.ToString();
    }

    public override string ToString() => $"program:{SortId} vs:{vertexShaderId} fs:{fragmentShaderId}";

    readonly struct Property<TType>(string name, int size, TType type, int location)
    {
        public readonly int Location = location;
        public override string ToString() => $"{size}@{Location} {type}x{size}";
    }
}