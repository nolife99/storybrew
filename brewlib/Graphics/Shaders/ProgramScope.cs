namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenTK.Graphics.OpenGL;

public class ProgramScope
{
    readonly List<ShaderStorageType> ssbos = [];
    readonly List<ShaderType> structs = [];
    readonly List<ShaderVariable> varyings = [], uniforms = [], vertexBuiltins = [], fragmentBuiltins = [];

    int lastId;
    string nextGenericTypeName => $"t_{lastId++:000}";
    string nextGenericVaryingName => $"v_{lastId++:000}";

    public ShaderType AddStruct()
    {
        ShaderType type = new(nextGenericTypeName);
        structs.Add(type);
        return type;
    }

    public ShaderStorageType AddSSBO(int bindingIndex)
    {
        ShaderStorageType type = new(nextGenericTypeName, bindingIndex);
        ssbos.Add(type);
        return type;
    }

    public ShaderVariable AddUniform(ShaderContext context, string name, ActiveUniformType shaderTypeName, int count = -1)
    {
        ShaderVariable uniform = new(context, name, shaderTypeName, count);
        uniforms.Add(uniform);
        return uniform;
    }

    public ShaderVariable AddVarying(ShaderContext context, ActiveUniformType shaderTypeName)
    {
        ShaderVariable varying = new(context, nextGenericVaryingName, shaderTypeName);
        varyings.Add(varying);
        return varying;
    }

    public ShaderVariable AddBuiltinVarying(ShaderContext context,
        string name,
        ActiveUniformType shaderTypeName,
        bool isFragmentShader)
    {
        ShaderVariable varying = new(context, name, shaderTypeName);
        if (isFragmentShader) fragmentBuiltins.Add(varying);
        else vertexBuiltins.Add(varying);

        return varying;
    }

    public void DeclareTypes(StringBuilder code)
    {
        foreach (var type in structs)
        {
            code.AppendLine(CultureInfo.InvariantCulture, $"struct {type.Name} {{");
            foreach (var field in type.Fields)
                code.AppendLine(CultureInfo.InvariantCulture, $"    {field.ShaderTypeName.GetString()} {field.Name};");

            code.AppendLine("};");
        }

        foreach (var type in ssbos)
        {
            code.AppendLine(CultureInfo.InvariantCulture,
                $"layout(binding = {type.BindingIndex}) buffer {ShaderStorageType.BlockName} {{");

            foreach (var field in type.Fields)
            {
                code.Append(CultureInfo.InvariantCulture, $"    {field.ShaderTypeName.GetString()} {field.Name}");
                if (field.ArrayCount == 0) code.AppendLine("[];");
                else if (field.ArrayCount != -1) code.AppendLine(CultureInfo.InvariantCulture, $"[{field.ArrayCount}];");
            }

            code.AppendLine(CultureInfo.InvariantCulture, $"}} {type.Name};");
        }
    }

    public void DeclareUniforms(StringBuilder code)
    {
        foreach (var uniform in uniforms)
        {
            code.Append(CultureInfo.InvariantCulture, $"uniform {uniform.ShaderTypeName.GetString()} {uniform.Name}");
            if (uniform.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{uniform.ArrayCount}]");
            code.AppendLine(";");
        }
    }

    public void DeclareVaryings(StringBuilder code, ShaderContext context, bool isFragmentShader)
    {
        if (isFragmentShader)
            for (var i = 0; i < fragmentBuiltins.Count; i++)
            {
                var varying = fragmentBuiltins[i];
                DeclareBuiltinVarying(varying, i);
            }
        else
            for (var i = 0; i < vertexBuiltins.Count; i++)
            {
                var varying = vertexBuiltins[i];
                DeclareBuiltinVarying(varying, i);
            }

        foreach (var varying in varyings)
            if (context.Uses(varying))
            {
                var flat = varying.ShaderTypeName.IsFlatType() ? "flat " : "";
                var varyingType = isFragmentShader ? "in" : "out";
                code.Append(CultureInfo.InvariantCulture,
                    $"{flat}{varyingType} {varying.ShaderTypeName.GetString()} {varying.Name}");

                if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
                code.AppendLine(";");
            }

        return;

        void DeclareBuiltinVarying(ShaderVariable varying, int index)
        {
            code.Append(CultureInfo.InvariantCulture,
                $"layout(location = {index}) out {varying.ShaderTypeName.GetString()} {varying.Name}");

            if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
            code.AppendLine(";");
        }
    }

    public void DeclareUnusedVaryingsAsVariables(StringBuilder code, ShaderContext context)
    {
        foreach (var varying in varyings)
            if (!context.Uses(varying))
            {
                code.Append(CultureInfo.InvariantCulture, $"{varying.ShaderTypeName.GetString()} {varying.Name}");
                if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
                code.AppendLine(";");
            }
    }
}