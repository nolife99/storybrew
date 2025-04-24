namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public class ProgramScope
{
    readonly List<ShaderType> types = [];
    readonly List<ShaderVariable> varyings = [], uniforms = [], vertexBuiltins = [], fragmentBuiltins = [];

    int lastId;
    string nextGenericTypeName => $"t_{lastId++:000}";
    string nextGenericVaryingName => $"v_{lastId++:000}";

    public ShaderType AddStruct()
    {
        ShaderType type = new(nextGenericTypeName);
        types.Add(type);
        return type;
    }

    public ShaderVariable AddUniform(ShaderContext context, string name, string shaderTypeName, int count = -1)
    {
        ShaderVariable uniform = new(context, name, shaderTypeName, count);
        uniforms.Add(uniform);
        return uniform;
    }

    public ShaderVariable AddVarying(ShaderContext context, string shaderTypeName)
    {
        ShaderVariable varying = new(context, nextGenericVaryingName, shaderTypeName);
        varyings.Add(varying);
        return varying;
    }

    public ShaderVariable AddBuiltinVarying(ShaderContext context, string name, string shaderTypeName, bool isFragmentShader)
    {
        ShaderVariable varying = new(context, name, shaderTypeName);
        if (isFragmentShader) fragmentBuiltins.Add(varying);
        else vertexBuiltins.Add(varying);

        return varying;
    }

    public void DeclareTypes(StringBuilder code)
    {
        foreach (var type in types)
        {
            code.AppendLine(CultureInfo.InvariantCulture, $"struct {type.Name} {{");
            foreach (var field in type.Fields)
                code.AppendLine(CultureInfo.InvariantCulture, $"    {field.ShaderTypeName} {field.Name};");

            code.AppendLine("};");
        }
    }

    public void DeclareUniforms(StringBuilder code)
    {
        foreach (var uniform in uniforms)
        {
            code.Append(CultureInfo.InvariantCulture, $"uniform {uniform.ShaderTypeName} {uniform.Name}");
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
                var varyingType = isFragmentShader ? "in" : "out";
                code.Append(CultureInfo.InvariantCulture, $"{varyingType} {varying.ShaderTypeName} {varying.Name}");
                if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
                code.AppendLine(";");
            }

        return;

        void DeclareBuiltinVarying(ShaderVariable varying, int index)
        {
            code.Append(CultureInfo.InvariantCulture, $"layout(location = {index}) out {varying.ShaderTypeName} {varying.Name}");
            if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
            code.AppendLine(";");
        }
    }

    public void DeclareUnusedVaryingsAsVariables(StringBuilder code, ShaderContext context)
    {
        foreach (var varying in varyings)
            if (!context.Uses(varying))
            {
                code.Append(CultureInfo.InvariantCulture, $"{varying.ShaderTypeName} {varying.Name}");
                if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
                code.AppendLine(";");
            }
    }
}