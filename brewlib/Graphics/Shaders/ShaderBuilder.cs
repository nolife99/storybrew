namespace BrewLib.Graphics.Shaders;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Util;

public class ShaderBuilder
{
    public readonly ShaderContext Context = new();
    public readonly ShaderVariable GlPosition, GlPointSize, GlFragColor, GlFragDepth, GlDrawID;
    readonly ProgramScope ProgramScope = new();
    readonly ShaderPartScope VertexShaderScope = new("vs"), FragmentShaderScope = new("fs");
    public int MinVersion = 110;
    public VertexDeclaration VertexDeclaration;

    public ShaderSnippet VertexShader, FragmentShader;

    List<string> requiredExt = [];

    public ShaderBuilder(VertexDeclaration vertexDeclaration)
    {
        VertexDeclaration = vertexDeclaration;
        GlPosition = new(Context, "gl_Position", ActiveUniformType.FloatVec4);
        GlPointSize = new(Context, "gl_PointSize", ActiveUniformType.Float);
        GlFragColor = ProgramScope.AddBuiltinVarying(Context, "fragColor", ActiveUniformType.FloatVec4, true);
        GlFragDepth = new(Context, "gl_FragDepth", ActiveUniformType.Float);
        GlDrawID = new(Context, "gl_DrawIDARB", ActiveUniformType.Int);
    }

    public ShaderVariable AddUniform(string name, ActiveUniformType shaderTypeName, int count = -1)
        => ProgramScope.AddUniform(Context, name, shaderTypeName, count);

    public ShaderVariable AddVarying(ActiveUniformType shaderTypeName) => ProgramScope.AddVarying(Context, shaderTypeName);

    public ShaderVariable AddVertexVariable(ActiveUniformType shaderTypeName)
        => VertexShaderScope.AddVariable(Context, shaderTypeName);

    public ShaderVariable AddFragmentVariable(ActiveUniformType shaderTypeName)
        => FragmentShaderScope.AddVariable(Context, shaderTypeName);

    public void AddRequiredExtension(params string[] extensionName) => requiredExt.AddRange(extensionName);

    public ShaderStorageType AddSSBO(int bindingIndex)
        => ProgramScope.AddSSBO(bindingIndex);

    public Shader Build(bool log = true)
    {
        Context.VertexDeclaration = VertexDeclaration;
        Context.MarkUsedVariables(() => FragmentShader.Generate(Context), GlPosition, GlPointSize, GlFragDepth);

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

        return new(vertexShaderCode.ToString(), fragmentShaderCode.ToString());
    }

    ReadOnlySpan<char> buildCommon()
    {
        var code = StringHelper.StringBuilderPool.Retrieve();
        code.AppendLine(CultureInfo.InvariantCulture,
            $"#version {int.Max(MinVersion, int.Max(VertexShader.MinVersion, FragmentShader.MinVersion))}");

        foreach (var extensionName in requiredExt)
        {
            if (!GLFW.ExtensionSupported(extensionName))
                throw new NotSupportedException($"Required extension {extensionName} not supported");

            code.AppendLine(CultureInfo.InvariantCulture, $"#extension {extensionName} : require");
        }

        ProgramScope.DeclareTypes(code);

        var codeString = code.ToString();
        StringHelper.StringBuilderPool.Release(code);
        return codeString;
    }

    StringBuilder buildVertexShader()
    {
        StringBuilder code = new();

        // Attributes

        ProgramScope.DeclareVaryings(code, Context, false);

        foreach (var attribute in VertexDeclaration)
            code.AppendLine(CultureInfo.InvariantCulture, $"in {attribute.ShaderTypeName} {attribute.Name};");

        ProgramScope.DeclareUniforms(code);

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

        ProgramScope.DeclareVaryings(code, Context, true);
        ProgramScope.DeclareUniforms(code);
        FragmentShader.GenerateFunctions(code);

        // Main function

        code.AppendLine("void main() {");
        FragmentShaderScope.DeclareVariables(code);
        Context.GenerateCode(code, () => FragmentShader.Generate(Context));
        code.AppendLine("}");
        return code;
    }
}