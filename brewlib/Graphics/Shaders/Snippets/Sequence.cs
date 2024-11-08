namespace BrewLib.Graphics.Shaders.Snippets;

using System.Collections.Generic;
using System.Linq;
using System.Text;

public class Sequence(params ShaderSnippet[] snippets) : ShaderSnippet
{
    public override IEnumerable<string> RequiredExtensions => snippets.SelectMany(t => t.RequiredExtensions);
    public override int MinVersion => snippets.Select(t => t.MinVersion).Prepend(base.MinVersion).Max();

    public override void GenerateFunctions(StringBuilder code)
    {
        foreach (var t in snippets) t.GenerateFunctions(code);
    }
    public override void Generate(ShaderContext context)
    {
        foreach (var t in snippets) t.Generate(context);
    }
}