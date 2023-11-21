using System;
using System.Collections.Generic;

namespace BrewLib.Graphics.Shaders
{
    public class ShaderType(string name)
    {
        readonly HashSet<Field> fields = [];

        public readonly string Name = name;
        public IEnumerable<Field> Fields => fields;

        public Field AddField(string name, string shaderTypeName)
        {
            var field = new Field(name, shaderTypeName);
            fields.Add(field);
            return field;
        }
        public ShaderVariable FieldAsVariable(ShaderVariable variable, Field field)
        {
            if (variable == null) return null;

            if (variable.ShaderTypeName != Name) throw new InvalidOperationException();
            if (!fields.Contains(field)) throw new InvalidOperationException();

            return new ShaderFieldVariable(variable.Context, variable, field);
        }
        public class Field(string name, string shaderTypeName)
        {
            public readonly string Name = name;
            public readonly string ShaderTypeName = shaderTypeName;
        }
    }
}