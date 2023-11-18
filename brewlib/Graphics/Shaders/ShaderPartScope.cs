using System.Collections.Generic;
using System.Text;

namespace BrewLib.Graphics.Shaders
{
    public class ShaderPartScope(string variablePrefix)
    {
        int lastId;
        string nextGenericName => $"_{variablePrefix}_{lastId++:000}";

        readonly string variablePrefix = variablePrefix;
        readonly List<ShaderVariable> variables = [];

        public ShaderVariable AddVariable(ShaderContext context, string shaderTypeName)
        {
            var variable = new ShaderVariable(context, nextGenericName, shaderTypeName);
            variables.Add(variable);
            return variable;
        }
        public void DeclareVariables(StringBuilder code) => variables.ForEach(variable =>
        {
            code.Append($"{variable.ShaderTypeName} {variable.Name}");
            if (variable.ArrayCount != -1) code.Append($"[{variable.ArrayCount}]");
            code.AppendLine(";");
        });
    }
}