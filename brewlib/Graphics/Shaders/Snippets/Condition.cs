using System;
using System.Collections.Generic;
using System.Text;

namespace BrewLib.Graphics.Shaders.Snippets
{
    public class Condition(Func<string> expression, ShaderSnippet trueSnippet, ShaderSnippet falseSnippet = null) : ShaderSnippet
    {
        readonly Func<string> expression = expression;
        readonly ShaderSnippet trueSnippet = trueSnippet, falseSnippet = falseSnippet;

        public override IEnumerable<string> RequiredExtensions
        {
            get
            {
                foreach (var requiredExtension in trueSnippet.RequiredExtensions)
                    yield return requiredExtension;

                if (falseSnippet is not null) foreach (var requiredExtension in falseSnippet.RequiredExtensions)
                        yield return requiredExtension;
            }
        }

        public override int MinVersion => falseSnippet is not null ?
            Math.Max(trueSnippet.MinVersion, falseSnippet.MinVersion) : trueSnippet.MinVersion;

        public override void GenerateFunctions(StringBuilder code)
        {
            trueSnippet.GenerateFunctions(code);
            falseSnippet?.GenerateFunctions(code);
        }
        public override void Generate(ShaderContext context) => context.Condition(expression, trueSnippet, falseSnippet);
    }
}