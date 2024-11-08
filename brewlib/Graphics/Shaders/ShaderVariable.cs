namespace BrewLib.Graphics.Shaders;

using System;

public class ShaderVariable
{
    public readonly int ArrayCount;
    public readonly ShaderContext Context;
    public readonly string Name, ShaderTypeName;

    readonly Reference reference;

    public ShaderVariable(ShaderContext context, string name, string shaderTypeName = null, int count = -1)
    {
        Context = context;
        Name = name;
        ShaderTypeName = shaderTypeName;
        ArrayCount = count;

        reference = new(this);
    }

    public virtual Reference Ref
    {
        get
        {
            RecordDependency();
            return reference;
        }
    }

    public void Assign(ShaderVariable value, string components = null) => Context.Assign(this, value, components);

    public void Assign(Func<string> expression, string components = null)
        => Context.Assign(this, expression, components);

    protected void RecordDependency() => Context.RecordDependency(this);

    public override string ToString()
    {
        var arrayTag = ArrayCount != -1 ? $"[{ArrayCount}]" : "";
        return $"{ShaderTypeName} {Name}{arrayTag}";
    }

    public class Reference(ShaderVariable variable)
    {
        readonly ShaderVariable variable = variable;

        public virtual string this[string index] => $"{variable.Name}[{index}]";
        public override string ToString() => variable.Name;
    }
}