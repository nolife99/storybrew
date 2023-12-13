using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

namespace StorybrewEditor.Scripting;

public class ScriptCompiler
{
    static readonly List<string> environmentDirectories =
    [
        Path.GetDirectoryName(typeof(object).Assembly.Location),
        Path.GetDirectoryName(typeof(Brush).Assembly.Location),
        Environment.CurrentDirectory
    ];

    public static Assembly Compile(AssemblyLoadContext context, IEnumerable<string> sourcePaths, string asmName, IEnumerable<string> referencedAssemblies)
    {
        Dictionary<SyntaxTree, KeyValuePair<string, SourceText>> trees = [];
        foreach (var src in sourcePaths) using (FileStream sourceStream = new(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var sourceText = SourceText.From(sourceStream, canBeEmbedded: true);
            trees[SyntaxFactory.ParseSyntaxTree(sourceText, new CSharpParseOptions(LanguageVersion.Latest))] = new(src, sourceText);
        }
        List<MetadataReference> references = [];

        var loadedAsm = AppDomain.CurrentDomain.GetAssemblies().Select(d => d.Location);
        context ??= AssemblyLoadContext.Default;

        foreach (var referencedAssembly in referencedAssemblies)
        {
            var asmPath = referencedAssembly;
            try
            {
                if (Path.IsPathRooted(asmPath)) using (FileStream stream = new(asmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (!loadedAsm.Contains(asmPath)) context.LoadFromStream(stream);
                    stream.Position = 0;
                    references.Add(MetadataReference.CreateFromStream(stream));
                }
                else
                {
                    var isExist = false;
                    for (var i = 0; i < environmentDirectories.Count; ++i)
                    {
                        var actualAsmPath = Path.Combine(environmentDirectories[i], referencedAssembly);
                        if (!File.Exists(actualAsmPath)) continue;
                        isExist = true;
                        asmPath = actualAsmPath;
                        break;
                    }

                    if (isExist) using (FileStream stream = new(asmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (!loadedAsm.Contains(asmPath)) context.LoadFromStream(stream);
                        stream.Position = 0;
                        references.Add(MetadataReference.CreateFromStream(stream));
                    }
                    else throw new IOException($"Could not resolve dependency: \"{referencedAssembly}\". " +
                        $"Searched directories: {string.Join(";", environmentDirectories.Select(k => $"\"{k}\""))}");
                }
            }
            catch (Exception e)
            {
                StringBuilder message = new("Compilation error\n\n");
                message.AppendLine(e.ToString());
                throw new ScriptCompilationException(message.ToString());
            }
        }

        using MemoryStream assemblyStream = new();
        var result = CSharpCompilation.Create(Path.GetFileName(asmName), trees.Keys, references, new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release))
            .Emit(assemblyStream, embeddedTexts: trees.Values.Select(k => EmbeddedText.FromSource(k.Key, k.Value)), options: new(debugInformationFormat: DebugInformationFormat.Embedded));

        if (result.Success)
        {
            assemblyStream.Position = 0;
            return context.LoadFromStream(assemblyStream);
        }

        var failureGroup = result.Diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error).Reverse().GroupBy(k =>
        {
            if (k.Location.SourceTree is null) return "";
            if (trees.TryGetValue(k.Location.SourceTree, out var path)) return path.Key;
            return "";
        }).ToDictionary(k => k.Key, k => k);

        StringBuilder error = new("Compilation error\n\n");
        foreach (var kvp in failureGroup)
        {
            var file = kvp.Key;
            var diagnostics = kvp.Value;
            error.AppendLine(CultureInfo.InvariantCulture, $"{Path.GetFileName(file)}:");
            foreach (var diagnostic in diagnostics) error.AppendLine(CultureInfo.InvariantCulture, $"--{diagnostic}");
        }

        throw new ScriptCompilationException(error.ToString());
    }
}