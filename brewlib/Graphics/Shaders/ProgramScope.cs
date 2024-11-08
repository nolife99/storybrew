namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Util;

public class ProgramScope
{
    readonly List<ShaderType> types = [];
    readonly List<ShaderVariable> varyings = [], uniforms = [];

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

    public void DeclareTypes(StringBuilder code)
        => types.ForEach(type =>
        {
            code.AppendLine(CultureInfo.InvariantCulture, $"struct {type.Name} {{");
            foreach (var field in type.Fields)
                code.AppendLine(CultureInfo.InvariantCulture, $"    {field.ShaderTypeName} {field.Name};");
            code.AppendLine("};");
        });

    public void DeclareUniforms(StringBuilder code)
        => uniforms.ForEach(uniform =>
        {
            code.Append(CultureInfo.InvariantCulture, $"uniform {uniform.ShaderTypeName} {uniform.Name}");
            if (uniform.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{uniform.ArrayCount}]");
            code.AppendLine(";");
        });

    public void DeclareVaryings(StringBuilder code, ShaderContext context)
        => varyings.ForEach(varying =>
        {
            code.Append(CultureInfo.InvariantCulture, $"varying {varying.ShaderTypeName} {varying.Name}");
            if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
            code.AppendLine(";");
        }, context.Uses);

    public void DeclareUnusedVaryingsAsVariables(StringBuilder code, ShaderContext context)
        => varyings.ForEach(varying =>
        {
            code.Append(CultureInfo.InvariantCulture, $"{varying.ShaderTypeName} {varying.Name}");
            if (varying.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{varying.ArrayCount}]");
            code.AppendLine(";");
        }, varying => !context.Uses(varying));
}