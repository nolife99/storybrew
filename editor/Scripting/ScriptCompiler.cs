namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using BrewLib.Memory;
using BrewLib.Util;
using Collections.Pooled;
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
        IEnumerable<string> referencedAssemblies,
        CancellationTokenSource token)
    {
        var tokenSource = token?.Token ?? CancellationToken.None;

        using PooledDictionary<SyntaxTree, (string SourcePath, SourceText SourceText)> trees = new();
        foreach (var src in sourcePaths)
        {
            using var sourceStream = File.OpenRead(src);
            var sourceText = SourceText.From(sourceStream, canBeEmbedded: true);
            trees[CSharpSyntaxTree.ParseText(sourceText, new(LanguageVersion.Preview), cancellationToken: tokenSource)] =
                (src, sourceText);
        }

        EmitResult result;
        using (var assemblyStream = Pool.PooledMemoryStreamManager.GetStream())
        {
            using PooledList<MetadataReference> assemblies = new();
            foreach (var asmPath in referencedAssemblies)
            {
                using var stream = File.OpenRead(asmPath);
                if (!Project.DefaultAssemblies.Contains(asmPath))
                {
                    context.LoadFromStream(stream);
                    stream.Position = 0;
                }

                assemblies.Add(MetadataReference.CreateFromStream(stream));
            }

            result = CSharpCompilation
                .Create(asmName,
                    trees.Keys,
                    assemblies,
                    new(OutputKind.DynamicallyLinkedLibrary,
                        allowUnsafe: true,
                        optimizationLevel: OptimizationLevel.Release))
                .Emit(assemblyStream,
                    embeddedTexts: trees.Values.Select(k => EmbeddedText.FromSource(k.SourcePath, k.SourceText)),
                    options: new(debugInformationFormat: DebugInformationFormat.Embedded),
                    cancellationToken: tokenSource);

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
                error.Append(diagnostic);
                error.Append('\n');
            }
        }

        var errorStr = error.ToString();
        StringHelper.StringBuilderPool.Release(error);
        throw new ScriptCompilationException(errorStr);
    }
}