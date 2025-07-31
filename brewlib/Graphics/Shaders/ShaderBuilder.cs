namespace BrewLib.Graphics.Shaders;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using BrewLib.Util;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public class ShaderBuilder
{
    public const int MinVersion = 330;
    public readonly ShaderContext Context = new();
    public readonly ShaderVariable GlPosition, GlPointSize, GlFragColor, GlFragDepth, GlFragCoord, GlDrawID;
    readonly ProgramScope ProgramScope = new();

    readonly List<string> requiredExt = [];
    public readonly VertexDeclaration VertexDeclaration;
    readonly ShaderPartScope VertexShaderScope = new("vs"), FragmentShaderScope = new("fs");

    public ShaderSnippet VertexShader, FragmentShader;

    public ShaderBuilder(VertexDeclaration vertexDeclaration)
    {
        VertexDeclaration = vertexDeclaration;
        GlPosition = new(Context, "gl_Position", ActiveUniformType.FloatVec4);
        GlFragCoord = new(Context, "gl_FragCoord", ActiveUniformType.FloatVec4);
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

    public void AddRequiredExtension(params ReadOnlySpan<string> extensionName) => requiredExt.AddRange(extensionName);

    public ShaderStorageType AddSSBO(int bindingIndex) => ProgramScope.AddSSBO(bindingIndex);

    public Shader Build(bool log = false)
    {
        Context.VertexDeclaration = VertexDeclaration;
        Context.MarkUsedVariables(() => FragmentShader.Generate(Context), GlPosition, GlFragCoord, GlPointSize, GlFragDepth);

        using var commonCode = buildCommon();
        var commonCodeSpan = commonCode.AsReadOnlySpan();

        using var vertexShaderCode = buildVertexShader();
        using var fragmentShaderCode = buildFragmentShader();

        vertexShaderCode.InsertRange(0, commonCodeSpan);
        fragmentShaderCode.InsertRange(0, commonCodeSpan);

        if (log)
            Trace.WriteLine(
                $"--- VERTEX ---\n{vertexShaderCode.AsReadOnlySpan()}\n--- FRAGMENT ---\n{fragmentShaderCode.AsReadOnlySpan()}");

        return new(vertexShaderCode.AsReadOnlySpan().ToString(), fragmentShaderCode.AsReadOnlySpan().ToString());
    }

    TempList<char> buildCommon()
    {
        var code = StringHelper.Interpolate(CultureInfo.InvariantCulture,
            $"#version {int.Max(MinVersion, int.Max(VertexShader.MinVersion, FragmentShader.MinVersion))}\n");

        foreach (var extensionName in requiredExt)
        {
            if (!GLFW.ExtensionSupported(extensionName))
                throw new NotSupportedException($"Required extension {extensionName} not supported");

            code.Append($"#extension {extensionName} : require\n");
        }

        ProgramScope.DeclareTypes(ref code);

        return code;
    }

    TempList<char> buildVertexShader()
    {
        var code = TempList.Create<char>();

        // Attributes

        ProgramScope.DeclareVaryings(ref code, Context, false);

        foreach (var attribute in VertexDeclaration) code.Append($"in {attribute.ShaderTypeName} {attribute.Name};\n");

        ProgramScope.DeclareUniforms(ref code);

        VertexShader.GenerateFunctions(ref code);

        // Main function

        code.AddRange("void main() {");
        ProgramScope.DeclareUnusedVaryingsAsVariables(ref code, Context);
        VertexShaderScope.DeclareVariables(ref code);
        Context.GenerateCode(ref code, () => VertexShader.Generate(Context));
        code.AddRange("}\n");
        return code;
    }

    TempList<char> buildFragmentShader()
    {
        var code = TempList.Create<char>();

        ProgramScope.DeclareVaryings(ref code, Context, true);
        ProgramScope.DeclareUniforms(ref code);
        FragmentShader.GenerateFunctions(ref code);

        // Main function

        code.AddRange("void main() {");
        FragmentShaderScope.DeclareVariables(ref code);
        Context.GenerateCode(ref code, () => FragmentShader.Generate(Context));
        code.AddRange("}\n");
        return code;
    }
}