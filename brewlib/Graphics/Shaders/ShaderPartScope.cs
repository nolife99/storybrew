namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Globalization;
using OpenTK.Graphics.OpenGL;
using Tiny.PooledCollections.Generic.Temporary;
using Util;

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

    public void DeclareVariables(scoped ref TempList<char> code)
    {
        foreach (var variable in variables)
        {
            code.AddRange(variable.ShaderTypeName.GetString());
            code.Add(' ');
            code.Append(variable.Name);

            if (variable.ArrayCount != -1)
            {
                code.Add('[');
                code.AppendFormatted(variable.ArrayCount, provider: CultureInfo.InvariantCulture);
                code.Add(']');
            }

            code.Append(";\n");
        }
    }
}