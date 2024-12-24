namespace BrewLib.Graphics.Shaders;

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Util;

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

    public ShaderVariable AddUniform(string name, string shaderTypeName, int count = -1)
        => ProgramScope.AddUniform(Context, name, shaderTypeName, count);

    public ShaderVariable AddVarying(string shaderTypeName) => ProgramScope.AddVarying(Context, shaderTypeName);

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
        var code = StringHelper.StringBuilderPool.Retrieve();
        code.AppendLine(CultureInfo.InvariantCulture,
            $"#version {int.Max(MinVersion, int.Max(VertexShader.MinVersion, FragmentShader.MinVersion))}");

        foreach (var extensionName in FragmentShader.RequiredExtensions.Union(VertexShader.RequiredExtensions))
            code.AppendLine(CultureInfo.InvariantCulture, $"#extension {extensionName} : enable");

        ProgramScope.DeclareTypes(code);
        ProgramScope.DeclareUniforms(code);
        ProgramScope.DeclareVaryings(code, Context);

        var codeString = code.ToString();
        StringHelper.StringBuilderPool.Release(code);
        return codeString;
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