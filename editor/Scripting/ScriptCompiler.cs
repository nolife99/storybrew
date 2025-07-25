namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using BrewLib.Memory;
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
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InternalLoad")]
    static extern Assembly InternalLoad(AssemblyLoadContext c,
        ReadOnlySpan<byte> arrAssembly,
        ReadOnlySpan<byte> arrSymbols);

    public static Assembly Compile(AssemblyLoadContext context,
        IEnumerable<string> sourcePaths,
        string asmName,
        ReadOnlySpan<string> referencedAssemblies,
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

        EmitResult result;
        using (PoolingMemoryStream assemblyStream = new())
        {
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
                return InternalLoad(context, assemblyStream.WrittenSpan, default);
            }
        }

        using var error = TempList.Create("Compilation error\n \n");

        using var diagnosticGroups = TempDictionary.Create<string, ValueList<Diagnostic>>();
        foreach (var diagnostic in result.Diagnostics)
        {
            if (diagnostic.Severity is not DiagnosticSeverity.Error) continue;

            var key = "";
            if (diagnostic.Location.SourceTree is not null)
                if (trees.TryGetValue(diagnostic.Location.SourceTree, out var path))
                    key = path.SourcePath;

            if (!diagnosticGroups.TryGetValue(key, out var group))
            {
                group = ValueList.Create<Diagnostic>();
                diagnosticGroups.Add(key, group);
            }

            group.Add(diagnostic);
        }

        foreach (var kvp in diagnosticGroups)
        {
            error.Append($"{Path.GetFileName(kvp.Key.AsSpan())}:\n");

            using var diagnostics = kvp.Value;
            foreach (var diagnostic in diagnostics) error.Append($"--{diagnostic}\n");
        }

        throw new ScriptCompilationException(error.AsReadOnlySpan().ToString());
    }
}