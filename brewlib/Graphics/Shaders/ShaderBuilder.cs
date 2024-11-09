namespace BrewLib.Graphics.Shaders;

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

public class ShaderBuilder
{
    readonly ShaderContext Context = new();
    public readonly ShaderVariable GlPosition, GlPointSize, GlFragColor, GlFragDepth;
    readonly ProgramScope ProgramScope = new();
    readonly ShaderPartScope VertexShaderScope = new("vs"), FragmentShaderScope = new("fs");
    public int MinVersion = 110;
    public VertexDeclaration VertexDeclaration;

    public ShaderSnippet VertexShader, FragmentShader;

    public ShaderBuilder(VertexDeclaration vertexDeclaration)
    {
        VertexDeclaration = vertexDeclaration;
        GlPosition = new(Context, "gl_Position", "vec4");
        GlPointSize = new(Context, "gl_PointSize", "float");
        GlFragColor = new(Context, "gl_FragColor", "vec4");
        GlFragDepth = new(Context, "gl_FragDepth", "float");
    }

    public ShaderType AddStruct() => ProgramScope.AddStruct();

    public ShaderVariable AddUniform(string name, string shaderTypeName, int count = -1)
        => ProgramScope.AddUniform(Context, name, shaderTypeName, count);

    public ShaderVariable AddVarying(string shaderTypeName) => ProgramScope.AddVarying(Context, shaderTypeName);

    public ShaderVariable AddVertexVariable(string shaderTypeName) => VertexShaderScope.AddVariable(Context, shaderTypeName);

    public ShaderVariable AddFragmentVariable(string shaderTypeName) => FragmentShaderScope.AddVariable(Context, shaderTypeName);

    public Shader Build(bool log = false)
    {
        Context.VertexDeclaration = VertexDeclaration;
        Context.MarkUsedVariables(() => FragmentShader.Generate(Context), GlPointSize, GlFragColor, GlFragDepth);

        var commonCode = buildCommon();
        var vertexShaderCode = buildVertexShader().Insert(0, commonCode);
        var fragmentShaderCode = buildFragmentShader().Insert(0, commonCode);

        if (log)
        {
            Trace.WriteLine("--- VERTEX ---");
            Trace.WriteLine(vertexShaderCode);

            Trace.WriteLine("--- FRAGMENT ---");
            Trace.WriteLine(fragmentShaderCode);
        }

        return new(vertexShaderCode, fragmentShaderCode);
    }

    ReadOnlySpan<char> buildCommon()
    {
        StringBuilder code = new();
        code.AppendLine(CultureInfo.InvariantCulture,
            $"#version {Math.Max(MinVersion, Math.Max(VertexShader.MinVersion, FragmentShader.MinVersion))}");

        var extensions = FragmentShader.RequiredExtensions.Union(VertexShader.RequiredExtensions);
        foreach (var extensionName in extensions)
            code.AppendLine(CultureInfo.InvariantCulture, $"#extension {extensionName} : enable");

        code.AppendLine("#ifdef GL_ES");
        code.AppendLine("    precision lowp float;");
        code.AppendLine("#endif");

        ProgramScope.DeclareTypes(code);
        ProgramScope.DeclareUniforms(code);
        ProgramScope.DeclareVaryings(code, Context);

        return code.ToString();
    }
    StringBuilder buildVertexShader()
    {
        StringBuilder code = new();

        // Attributes
        foreach (var attribute in VertexDeclaration)
            code.AppendLine(CultureInfo.InvariantCulture, $"attribute {attribute.ShaderTypeName} {attribute.Name};");

        VertexShader.GenerateFunctions(code);

        // Main function

        code.AppendLine("void main() {");
        ProgramScope.DeclareUnusedVaryingsAsVariables(code, Context);
        VertexShaderScope.DeclareVariables(code);
        Context.GenerateCode(code, () => VertexShader.Generate(Context));
        code.AppendLine("}");
        return code;
    }
    StringBuilder buildFragmentShader()
    {
        StringBuilder code = new();
        FragmentShader.GenerateFunctions(code);

        // Main function

        code.AppendLine("void main() {");
        FragmentShaderScope.DeclareVariables(code);
        Context.GenerateCode(code, () => FragmentShader.Generate(Context));
        code.AppendLine("}");
        return code;
    }
}