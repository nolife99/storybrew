namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;

public class ShaderType(string name)
{
    readonly HashSet<Field> fields = [];

    public string Name => name;
    public IEnumerable<Field> Fields => fields;

    public class Field(string name, string shaderTypeName)
    {
        public string Name => name;
        public string ShaderTypeName => shaderTypeName;
    }
}