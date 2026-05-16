namespace BrewLib.Graphics.Shaders;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class ShaderType(string name)
{
    readonly List<Field> fields = [];

    public readonly string Name = name;
    public ReadOnlySpan<Field> Fields => CollectionsMarshal.AsSpan(fields);

    public Field AddField(string name, ShaderValueType shaderTypeName, int arrayCount = -1)
    {
        Field field = new(name, shaderTypeName, arrayCount);
        fields.Add(field);
        return field;
    }

    public ShaderVariable FieldAsVariable(ShaderVariable variable, Field field)
        => variable is null ? null :
            !fields.Contains(field) ? throw new InvalidOperationException() :
            (ShaderVariable)new ShaderFieldVariable(variable.Context, variable, field);

    public readonly record struct Field(string Name, ShaderValueType ShaderTypeName, int ArrayCount);
}

public class ShaderStorageType(string name, int bindingIndex) : ShaderType(name)
{
    static int nextGenericTypeName;

    public static string BlockName => $"ssbo{nextGenericTypeName++}";
    public int BindingIndex => bindingIndex;
}
