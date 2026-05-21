namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using BrewLib.Memory;
using BrewLib.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using SixLabors.ImageSharp.Memory;
using Storyboarding;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;
using Tiny.PooledCollections.Generic.Value;

public static class ScriptCompiler
{
    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    static extern Assembly InternalLoad(AssemblyLoadContext c,
        ReadOnlySpan<byte> arrAssembly,
        ReadOnlySpan<byte> arrSymbols);

    public static ScriptProcessingResult<Assembly> Compile(AssemblyLoadContext context,
        IEnumerable<string> sourcePaths,
        ReadOnlySpan<string> referencedAssemblies,
        CancellationTokenSource token)
    {
        var tokenSource = token?.Token ?? CancellationToken.None;

        using var trees = ValueDictionary.Create<SyntaxTree, (string SourcePath, SourceText SourceText)>();
        foreach (var src in sourcePaths)
        {
            using FileStream sourceStream = new(src, FileMode.Open, FileAccess.Read, FileShare.Read, 0);
            var sourceText = SourceText.From(sourceStream, canBeEmbedded: true);

            trees.Add(CSharpSyntaxTree.ParseText(sourceText,
                    new(LanguageVersion.Preview),
                    cancellationToken: tokenSource),
                (src, sourceText));
        }

        using var assemblies = ValueList.Create<AssemblyMetadata>();
        foreach (var asmPath in referencedAssemblies)
        {
            FileStream stream = null;
            try
            {
                stream = new(asmPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    0,
                    FileOptions.SequentialScan);

                if (!Project.DefaultAssemblies.Contains(asmPath))
                {
                    using var memory = MemoryAllocator.Default.Allocate<byte>((int)stream.Length);
                    var writtenSpan = memory.Memory.Span;

                    stream.ReadExactly(writtenSpan);
                    InternalLoad(context, writtenSpan, default);

                    stream.Position = 0;
                }

                assemblies.Add(AssemblyMetadata.CreateFromStream(stream, PEStreamOptions.PrefetchMetadata));
            }
            catch (Exception e)
            {
                return new(e);
            }
            finally
            {
                stream?.Dispose();
            }
        }

        EmitResult compilation;

        using (PoolingMemoryStream assemblyStream = new())
        using (PoolingMemoryStream pdbStream = new())
        {
            compilation = CSharpCompilation
                .Create(null,
                    trees.Keys,
                    assemblies.Select(s => s.GetReference()),
                    new(OutputKind.DynamicallyLinkedLibrary,
                        allowUnsafe: true,
                        optimizationLevel: OptimizationLevel.Release,
                        concurrentBuild: false))
                .Emit(assemblyStream,
                    pdbStream,
                    embeddedTexts: trees.Values.Select(k => EmbeddedText.FromSource(k.SourcePath, k.SourceText)),
                    options: new(debugInformationFormat: DebugInformationFormat.PortablePdb),
                    cancellationToken: tokenSource);

            if (compilation.Success)
            {
                foreach (var ass in assemblies) ass.Dispose();
                return new(InternalLoad(context, assemblyStream.WrittenSpan, pdbStream.WrittenSpan));
            }
        }

        using var error = TempList.Create("Compilation error\n");

        using var diagnosticGroups = TempDictionary.Create<string, ValueList<Diagnostic>>();
        foreach (var diagnostic in compilation.Diagnostics)
        {
            if (diagnostic.Severity is not DiagnosticSeverity.Error) continue;

            var key = "";
            if (diagnostic.Location.SourceTree is not null)
                if (trees.TryGetValue(diagnostic.Location.SourceTree, out var path))
                    key = path.SourcePath;

            ref var group = ref diagnosticGroups.GetValueRefOrNullRef(key);
            if (Unsafe.IsNullRef(ref group))
            {
                var localGroup = ValueList.Create<Diagnostic>();
                localGroup.Add(diagnostic);

                diagnosticGroups.Add(key, localGroup);
                continue;
            }

            group.Add(diagnostic);
        }

        foreach (var kvp in diagnosticGroups)
        {
            error.Append($"{Path.GetFileName(kvp.Key.AsSpan())}:\n");

            using var diagnostics = kvp.Value;
            foreach (var diagnostic in diagnostics) error.Append($"--{diagnostic}\n");
        }

        foreach (var ass in assemblies) ass.Dispose();
        tokenSource.ThrowIfCancellationRequested();

        return new(new ScriptCompilationException(error.AsReadOnlySpan().ToString()));
    }
}