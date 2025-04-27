namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenTK.Graphics.OpenGL;

public class ShaderPartScope(string variablePrefix)
{
    readonly List<ShaderVariable> variables = [];
    int lastId;
    string nextGenericName => $"_{variablePrefix}_{lastId++:000}";

    public ShaderVariable AddVariable(ShaderContext context, ActiveUniformType shaderTypeName)
    {
        ShaderVariable variable = new(context, nextGenericName, shaderTypeName);
        variables.Add(variable);
        return variable;
    }

    public void DeclareVariables(StringBuilder code)
    {
        foreach (var variable in variables)
        {
            code.Append(CultureInfo.InvariantCulture, $"{variable.ShaderTypeName.GetString()} {variable.Name}");
            if (variable.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{variable.ArrayCount}]");
            code.AppendLine(";");
        }
    }
}