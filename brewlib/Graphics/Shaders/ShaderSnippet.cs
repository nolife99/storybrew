namespace BrewLib.Graphics.Shaders;

using System.Collections.Generic;
using System.Text;

public abstract class ShaderSnippet
{
    public virtual IEnumerable<string> RequiredExtensions
    {
        get { yield break; }
    }

    public virtual int MinVersion => 110;

    public virtual void GenerateFunctions(StringBuilder code) { }
    public virtual void Generate(ShaderContext context) => context.Comment(GetType().Name);
}