using System.Collections.Generic;
using System.Text;

namespace BrewLib.Graphics.Shaders.Snippets
{
    public class Sequence(params ShaderSnippet[] snippets) : ShaderSnippet
    {
        readonly ShaderSnippet[] snippets = snippets;

        public override IEnumerable<string> RequiredExtensions
        {
            get
            {
                for (var i = 0; i < snippets.Length; ++i) foreach (var requiredExtension in snippets[i].RequiredExtensions)
                    yield return requiredExtension;
            }
        }
        public override int MinVersion
        {
            get
            {
                var minVersion = base.MinVersion;
                for (var i = 0; i < snippets.Length; ++i) if (snippets[i].MinVersion > minVersion)
                    minVersion = snippets[i].MinVersion;

                return minVersion;
            }
        }

        public override void GenerateFunctions(StringBuilder code)
        {
            for (var i = 0; i < snippets.Length; ++i) snippets[i].GenerateFunctions(code);
        }
        public override void Generate(ShaderContext context)
        {
            for (var i = 0; i < snippets.Length; ++i) snippets[i].Generate(context);
        }
    }
}