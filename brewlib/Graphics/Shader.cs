using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using osuTK;
using osuTK.Graphics.OpenGL;

namespace BrewLib.Graphics;

public class Shader : IDisposable
{
    int vertexShaderId = -1, fragmentShaderId = -1, programId = -1;

    bool isInitialized, started;
    StringBuilder log = new();

    Dictionary<string, Property<ActiveAttribType>> attributes;
    Dictionary<string, Property<ActiveUniformType>> uniforms;

    public string Log
    {
        get
        {
            if (!isInitialized) return "";
            if (log.Length == 0) log.AppendLine(GL.GetProgramInfoLog(programId));
            return log.ToString();
        }
    }
    public int SortId => programId;

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
    public void Begin()
    {
        if (started) throw new InvalidOperationException("Already started");

        DrawState.ProgramId = programId;
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
        var identifier = GetUniformIdentifier(name, index, field);

        if (uniforms.TryGetValue(identifier, out var property)) return property.Location;
        return -1;
    }
    public int GetUniformLocation(string name, int index = -1, string field = null)
    {
        var location = TryGetUniformLocation(name, index, field);

        if (location < 0) throw new ArgumentException($"{name} isn't a valid uniform identifier");
        return location;
    }
    public bool HasUniform(string name, int index = -1, string field = null)
        => uniforms.ContainsKey(GetUniformIdentifier(name, index, field));

    public static string GetUniformIdentifier(string name, int index, string field)
        => name + (index >= 0 ? "[" + index + "]" : "") + (field is not null ? "." + field : "");

    void initialize(StringBuilder vertexShaderCode, StringBuilder fragmentShaderCode)
    {
        Dispose(true);

        vertexShaderId = compileShader(ShaderType.VertexShader, vertexShaderCode.ToString());
        fragmentShaderId = compileShader(ShaderType.FragmentShader, fragmentShaderCode.ToString());

        if (vertexShaderId == -1 || fragmentShaderId == -1) return;

        programId = linkProgram();
        isInitialized = programId != -1;
    }
    int compileShader(ShaderType type, string code)
    {
        var id = GL.CreateShader(type);
        GL.ShaderSource(id, code);
        GL.CompileShader(id);

        GL.GetShader(id, ShaderParameter.CompileStatus, out var compileStatus);
        if (compileStatus == 0)
        {
            log.AppendLine(CultureInfo.InvariantCulture, $"--- {type} ---\n{addLineExtracts(GL.GetShaderInfoLog(id), code)}");
            return -1;
        }

        return id;
    }
    int linkProgram()
    {
        var id = GL.CreateProgram();
        GL.AttachShader(id, vertexShaderId);
        GL.AttachShader(id, fragmentShaderId);
        GL.LinkProgram(id);

        GL.GetProgram(id, GetProgramParameterName.LinkStatus, out var linkStatus);
        if (linkStatus == 0)
        {
            log.AppendLine(GL.GetProgramInfoLog(id));
            return -1;
        }

        return id;
    }
    void retrieveAttributes()
    {
        GL.GetProgram(programId, GetProgramParameterName.ActiveAttributes, out var attributeCount);

        attributes = new(attributeCount);
        for (var i = 0; i < attributeCount; ++i)
        {
            var name = GL.GetActiveAttrib(programId, i, out var size, out var type);
            var location = GL.GetAttribLocation(programId, name);
            attributes[name] = new(name, size, type, location);
        }
    }
    void retrieveUniforms()
    {
        GL.GetProgram(programId, GetProgramParameterName.ActiveUniforms, out var uniformCount);

        uniforms = new(uniformCount);
        for (var i = 0; i < uniformCount; ++i)
        {
            var name = GL.GetActiveUniform(programId, i, out var size, out var type);
            uniforms[name] = new(name, size, type, GL.GetUniformLocation(programId, name));
        }
    }

    ~Shader() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (isInitialized) isInitialized = false;
        else return;

        if (started) End();

        if (programId != -1) GL.DeleteProgram(programId);
        if (vertexShaderId != -1) GL.DeleteShader(vertexShaderId);
        if (fragmentShaderId != -1) GL.DeleteShader(fragmentShaderId);

        if (disposing)
        {
            programId = -1;
            vertexShaderId = -1;
            fragmentShaderId = -1;

            attributes.Clear();
            attributes = null;

            uniforms.Clear();
            uniforms = null;
        }
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
            if (match.Success)
            {
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
        }
        return sb.ToString();
    }
    public override string ToString() => $"program:{programId} vs:{vertexShaderId} fs:{fragmentShaderId}";

    struct Property<TType>(string name, int size, TType type, int location)
    {
        public string Name = name;
        public int Size = size, Location = location;
        public TType Type = type;

        public override readonly string ToString() => $"{size}@{location} {type}x{size}";
    }
}