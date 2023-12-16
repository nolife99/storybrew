using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BrewLib.Util;

namespace BrewLib.Graphics.Shaders;

public class ShaderPartScope(string variablePrefix)
{
    int lastId;
    string nextGenericName => $"_{variablePrefix}_{lastId++:000}";

    readonly List<ShaderVariable> variables = [];

    public ShaderVariable AddVariable(ShaderContext context, string shaderTypeName)
    {
        ShaderVariable variable = new(context, nextGenericName, shaderTypeName);
        variables.Add(variable);
        return variable;
    }
    public void DeclareVariables(StringBuilder code) => variables.ForEachUnsafe(variable =>
    {
        code.Append(CultureInfo.InvariantCulture, $"{variable.ShaderTypeName} {variable.Name}");
        if (variable.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{variable.ArrayCount}]");
        code.AppendLine(";");
    });
}