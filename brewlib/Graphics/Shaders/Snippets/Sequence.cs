namespace BrewLib.Graphics.Shaders.Snippets;

using System.Linq;
using Tiny.PooledCollections.Generic.Temporary;

public class Sequence(params ShaderSnippet[] snippets) : ShaderSnippet
{
    public override int MinVersion => snippets.Select(t => t.MinVersion).Prepend(base.MinVersion).Max();

    public override void GenerateFunctions(scoped ref TempList<char> code)
    {
        foreach (var t in snippets) t.GenerateFunctions(ref code);
    }

    public override void Generate(ShaderContext context)
    {
        foreach (var t in snippets) t.Generate(context);
    }
}