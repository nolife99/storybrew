namespace BrewLib.Graphics.Shaders;

using Tiny.PooledCollections.Generic.Temporary;

public abstract class ShaderSnippet
{
    public virtual int MinVersion => 330;

    public virtual void GenerateFunctions(scoped ref TempList<char> code) { }
    public virtual void Generate(ShaderContext context) { }
}