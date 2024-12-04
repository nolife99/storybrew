namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using BrewLib.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Storyboarding;

public static class ScriptCompiler
{
    public static Assembly Compile(AssemblyLoadContext context,
        IEnumerable<string> sourcePaths,
        string asmName,
        IEnumerable<string> referencedAssemblies)
    {
        Dictionary<SyntaxTree, (string SourcePath, SourceText SourceText)> trees = [];
        foreach (var src in sourcePaths)
        {
            using var sourceStream = File.OpenRead(src);
            var sourceText = SourceText.From(sourceStream, canBeEmbedded: true);
            trees[SyntaxFactory.ParseSyntaxTree(sourceText, new CSharpParseOptions(LanguageVersion.Preview))] = (src, sourceText);
        }

        EmitResult result;
        using (MemoryStream assemblyStream = new())
        {
            result = CSharpCompilation.Create(asmName, trees.Keys, referencedAssemblies.Select(asmPath =>
            {
                using var stream = File.OpenRead(asmPath);
                if (!Project.DefaultAssemblies.Contains(asmPath))
                {
                    AssemblyLoadContext.Default.LoadFromStream(stream);
                    stream.Position = 0;
                }

                return MetadataReference.CreateFromStream(stream);
            }), new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release)).Emit(
                assemblyStream, embeddedTexts: trees.Values.Select(k => EmbeddedText.FromSource(k.SourcePath, k.SourceText)),
                options: new(debugInformationFormat: DebugInformationFormat.Embedded));

            if (result.Success)
            {
                assemblyStream.Position = 0;
                return context.LoadFromStream(assemblyStream);
            }
        }

        var error = StringHelper.StringBuilderPool.Retrieve();
        error.Append("Compilation error\n \n");

        foreach (var diagnostics in result.Diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)
            .GroupBy(k =>
            {
                if (k.Location.SourceTree is null) return "";
                return trees.TryGetValue(k.Location.SourceTree, out var path) ? path.SourcePath : "";
            }))
        {
            error.Append(Path.GetFileName(diagnostics.Key.AsSpan()));
            error.Append(":\n");

            foreach (var diagnostic in diagnostics)
            {
                error.Append("--");
                error.AppendLine(diagnostic.ToString());
            }
        }

        var errorStr = error.ToString();
        StringHelper.StringBuilderPool.Release(error);
        throw new ScriptCompilationException(errorStr);
    }
}