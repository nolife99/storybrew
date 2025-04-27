namespace BrewLib.Graphics.Shaders;

using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;

public class ShaderType(string name)
{
    readonly List<Field> fields = [];

    public readonly string Name = name;
    public IEnumerable<Field> Fields => fields;

    public Field AddField(string name, ActiveUniformType shaderTypeName, int arrayCount = -1)
    {
        Field field = new(name, shaderTypeName, arrayCount);
        fields.Add(field);
        return field;
    }

    public ShaderVariable FieldAsVariable(ShaderVariable variable, Field field)
    {
        if (variable is null) return null;
        if (!fields.Contains(field)) throw new InvalidOperationException();

        return new ShaderFieldVariable(variable.Context, variable, field);
    }

    public class Field(string name, ActiveUniformType shaderTypeName, int arrayCount)
    {
        public string Name => name;
        public ActiveUniformType ShaderTypeName => shaderTypeName;
        public int ArrayCount => arrayCount;
    }
}

public class ShaderStorageType(string name, int bindingIndex) : ShaderType(name)
{
    static int nextGenericTypeName;

    public static string BlockName => $"ssbo{nextGenericTypeName++}";
    public int BindingIndex => bindingIndex;

    public bool Coherent { get; set; }
    public bool Volatile { get; set; }
    public bool Restrict { get; set; }
    public bool ReadOnly { get; set; }
    public bool WriteOnly { get; set; }
}