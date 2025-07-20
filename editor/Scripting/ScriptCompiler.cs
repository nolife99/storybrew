namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using BrewLib.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Storyboarding;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

public static class ScriptCompiler
{
    public static Assembly Compile(AssemblyLoadContext context,
        IEnumerable<string> sourcePaths,
        string asmName,
        IEnumerable<string> referencedAssemblies,
        CancellationTokenSource token)
    {
        var tokenSource = token?.Token ?? CancellationToken.None;

        using var trees = ValueDictionary<SyntaxTree, (string SourcePath, SourceText SourceText)>.Create();
        foreach (var src in sourcePaths)
        {
            using var sourceStream = File.OpenRead(src);
            var sourceText = SourceText.From(sourceStream, canBeEmbedded: true);

            trees.Add(CSharpSyntaxTree.ParseText(sourceText, new(LanguageVersion.Preview), cancellationToken: tokenSource),
                (src, sourceText));
        }

        EmitResult result;
        using (MemoryStream assemblyStream = new())
        {
            using var assemblies = ValueList.Create<MetadataReference>();
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

        using var error = TempList.Create("Compilation error\n \n".AsSpan());
        foreach (var diagnostics in result.Diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)
            .GroupBy(k =>
            {
                if (k.Location.SourceTree is null) return "";

                return trees.TryGetValue(k.Location.SourceTree, out var path) ? path.SourcePath : "";
            }))
        {
            error.Append($"{Path.GetFileName(diagnostics.Key.AsSpan())}:\n");
            foreach (var diagnostic in diagnostics) error.Append($"--{diagnostic.ToString()}\n");
        }

        throw new ScriptCompilationException(error.AsReadOnlySpan().ToString());
    }
}