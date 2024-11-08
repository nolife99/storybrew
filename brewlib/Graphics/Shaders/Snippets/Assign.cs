namespace BrewLib.Graphics.Shaders.Snippets;

using System;

public class Assign(ShaderVariable result, Func<string> expression, string components = null) : ShaderSnippet
{
    public Assign(ShaderVariable result, ShaderVariable value, string components = null) : this(result,
        value.Ref.ToString, components) { }

    public Assign(ShaderVariable result, VertexAttribute value, string components = null) : this(result,
        () => value.Name, components) { }

    public override void Generate(ShaderContext context)
    {
        base.Generate(context);
        result?.Assign(expression, components);
    }
}