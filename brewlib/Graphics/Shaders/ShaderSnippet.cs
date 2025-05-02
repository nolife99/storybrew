namespace BrewLib.Graphics.Shaders;

using System.Text;

public abstract class ShaderSnippet
{
    public virtual int MinVersion => 330;

    public virtual void GenerateFunctions(StringBuilder code) { }
    public virtual void Generate(ShaderContext context) { }
}