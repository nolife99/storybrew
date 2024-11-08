namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Globalization;
using System.Text;

public class ShaderPartScope(string variablePrefix)
{
    readonly List<ShaderVariable> variables = [];
    int lastId;
    string nextGenericName => $"_{variablePrefix}_{lastId++:000}";

    public ShaderVariable AddVariable(ShaderContext context, string shaderTypeName)
    {
        ShaderVariable variable = new(context, nextGenericName, shaderTypeName);
        variables.Add(variable);
        return variable;
    }

    public void DeclareVariables(StringBuilder code)
        => variables.ForEach(variable =>
        {
            code.Append(CultureInfo.InvariantCulture, $"{variable.ShaderTypeName} {variable.Name}");
            if (variable.ArrayCount != -1) code.Append(CultureInfo.InvariantCulture, $"[{variable.ArrayCount}]");
            code.AppendLine(";");
        });
}