using System;

namespace BrewLib.Graphics.Shaders.Snippets
{
    public class CustomSnippet(Action<ShaderContext> action) : ShaderSnippet
    {
        readonly Action<ShaderContext> action = action;

        public override void Generate(ShaderContext context)
        {
            base.Generate(context);
            action(context);
        }
    }
}