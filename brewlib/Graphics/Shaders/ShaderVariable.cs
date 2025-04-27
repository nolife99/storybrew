namespace BrewLib.Graphics.Shaders;

using System;
using OpenTK.Graphics.OpenGL;

public class ShaderVariable
{
    public readonly ShaderContext Context;
    readonly Reference reference;

    public ShaderVariable(ShaderContext context, string name, ActiveUniformType shaderTypeName, int count = -1)
    {
        Context = context;
        Name = name;
        ShaderTypeName = shaderTypeName;
        ArrayCount = count;

        reference = new(this);
    }

    public int ArrayCount { get; }
    public string Name { get; }
    public ActiveUniformType ShaderTypeName { get; }

    public virtual Reference Ref
    {
        get
        {
            RecordDependency();
            return reference;
        }
    }

    public void Assign(Func<string> expression, string components = null) => Context.Assign(this, expression, components);

    protected void RecordDependency() => Context.RecordDependency(this);

    public override string ToString()
    {
        var arrayTag = ArrayCount == 0 ? "[]" :
            ArrayCount != -1 ? $"[{ArrayCount}]" : "";

        return $"{ShaderTypeName.GetString()} {Name}{arrayTag}";
    }

    public class Reference(ShaderVariable variable)
    {
        public virtual string this[string index] => $"{variable.Name}[{index}]";

        public override string ToString() => variable.Name;
    }
}