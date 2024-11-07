using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace StorybrewEditor.Scripting;

public class ScriptCompiler
{
    static readonly string[] environmentDirectories =
    [
        Path.GetDirectoryName(typeof(object).Assembly.Location),
        Path.GetDirectoryName(typeof(Brush).Assembly.Location),
        Environment.CurrentDirectory
    ];

    public static Assembly Compile(AssemblyLoadContext context, IEnumerable<string> sourcePaths, string asmName, IEnumerable<string> referencedAssemblies)
    {
        Dictionary<SyntaxTree, (string SourcePath, SourceText SourceText)> trees = [];
        foreach (var src in sourcePaths) using (var sourceStream = File.OpenRead(src))
            {
                var sourceText = SourceText.From(sourceStream, canBeEmbedded: true);
                trees[SyntaxFactory.ParseSyntaxTree(sourceText, new CSharpParseOptions(LanguageVersion.Latest))] = (src, sourceText);
            }
        List<MetadataReference> references = [];

        var loadedAsm = AssemblyLoadContext.Default.Assemblies.Select(d => d.Location);
        context ??= AssemblyLoadContext.Default;

        foreach (var referencedAssembly in referencedAssemblies)
        {
            var asmPath = referencedAssembly;
            try
            {
                if (Path.IsPathRooted(asmPath)) using (var stream = File.OpenRead(asmPath))
                    {
                        if (!loadedAsm.Contains(asmPath)) context.LoadFromStream(stream);
                        stream.Position = 0;
                        references.Add(MetadataReference.CreateFromStream(stream));
                    }
                else
                {
                    var exists = false;
                    for (var i = 0; i < environmentDirectories.Length; ++i)
                    {
                        var actualAsmPath = Path.Combine(environmentDirectories[i], referencedAssembly);
                        if (!File.Exists(actualAsmPath)) continue;
                        exists = true;
                        asmPath = actualAsmPath;
                        break;
                    }

                    if (exists) using (var stream = File.OpenRead(asmPath))
                        {
                            if (!loadedAsm.Contains(asmPath)) context.LoadFromStream(stream);
                            stream.Position = 0;
                            references.Add(MetadataReference.CreateFromStream(stream));
                        }
                    else throw new IOException($"Could not resolve dependency: \"{referencedAssembly}\". " +
                        $"Searched directories: {string.Join(';', environmentDirectories.Select(k => $"\"{k}\""))}");
                }
            }
            catch (Exception e)
            {
                StringBuilder message = new("Compilation error\n\n");
                message.AppendLine(e.ToString());
            }
        }

        EmitResult result;
        using (MemoryStream assemblyStream = new())
        {
            result = CSharpCompilation.Create(asmName, trees.Keys, references,
                new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release))
                .Emit(assemblyStream, embeddedTexts: trees.Values.Select(k => EmbeddedText.FromSource(k.SourcePath, k.SourceText)),
                    options: new(debugInformationFormat: DebugInformationFormat.Embedded));

            if (result.Success)
            {
                assemblyStream.Position = 0;
                return context.LoadFromStream(assemblyStream);
            }
        }

        StringBuilder error = new("Compilation error\n\n");
        string key = null;

        foreach (var diagnostic in result.Diagnostics) if (diagnostic.Severity is DiagnosticSeverity.Error)
            {
                var nextKey = trees[diagnostic.Location.SourceTree].SourcePath;
                if (key != nextKey)
                {
                    key = nextKey;
                    error.AppendLine(CultureInfo.InvariantCulture, $"{Path.GetFileName(key)}:");
                }
                error.AppendLine(CultureInfo.InvariantCulture, $"--{diagnostic}");
            }
        throw new ScriptCompilationException(error.ToString());
    }
}