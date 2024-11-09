namespace BrewLib.Graphics.Shaders;

internal class ShaderFieldVariable(ShaderContext context, ShaderVariable baseVariable, ShaderType.Field field)
    : ShaderVariable(context, $"{baseVariable.Name}_field_{field.Name}", field.ShaderTypeName, baseVariable.ArrayCount)
{
    readonly Reference reference = new(baseVariable, field);

    public override ShaderVariable.Reference Ref
    {
        get
        {
            RecordDependency();
            return reference;
        }
    }

    public new class Reference(ShaderVariable variable, ShaderType.Field field) : ShaderVariable.Reference(variable)
    {
        readonly ShaderType.Field field = field;

        protected override string this[string index] => $"{base[index]}.{field.Name}";
        public override string ToString() => $"{base.ToString()}.{field.Name}";
    }
}