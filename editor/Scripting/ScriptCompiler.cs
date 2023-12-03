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
using System.Text;

namespace StorybrewEditor.Scripting;

public class ScriptCompiler
{
    static readonly string[] environmentDirectories =
    [
        Path.GetDirectoryName(typeof(object).Assembly.Location),
        Path.GetDirectoryName(typeof(Brush).Assembly.Location),
        Environment.CurrentDirectory
    ];

    public static byte[] Compile(IEnumerable<string> sourcePaths, string asmName, IEnumerable<string> referencedAssemblies)
    {
        Dictionary<SyntaxTree, KeyValuePair<string, SourceText>> trees = [];
        foreach (var src in sourcePaths) using (var sourceStream = File.OpenRead(src))
        {
            var sourceText = SourceText.From(sourceStream, canBeEmbedded: true);
            trees[SyntaxFactory.ParseSyntaxTree(sourceText, new CSharpParseOptions(LanguageVersion.Latest))] = new(src, sourceText);
        }
        HashSet<MetadataReference> references = [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];

        foreach (var referencedAssembly in referencedAssemblies)
        {
            var asmPath = referencedAssembly;
            try
            {
                if (Path.IsPathRooted(asmPath)) references.Add(MetadataReference.CreateFromFile(asmPath));
                else
                {
                    var isExist = false;
                    for (var i = 0; i < environmentDirectories.Length; ++i)
                    {
                        var actualAsmPath = Path.Combine(environmentDirectories[i], referencedAssembly);
                        if (!File.Exists(actualAsmPath)) continue;
                        isExist = true;
                        asmPath = actualAsmPath;
                        break;
                    }

                    if (isExist) references.Add(MetadataReference.CreateFromFile(asmPath));
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

        var compilation = CSharpCompilation.Create(Path.GetFileName(asmName), trees.Keys, references, 
            new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));

        MemoryStream assemblyStream = new();

        var result = compilation.Emit(assemblyStream, embeddedTexts: trees.Values.Select(k => EmbeddedText.FromSource(k.Key, k.Value)), 
            options: new(debugInformationFormat: DebugInformationFormat.Embedded));

        if (result.Success)
        {
            trees.Clear();
            var arr = assemblyStream.ToArray();
            assemblyStream.Dispose();

            return arr;
        }
        assemblyStream.Dispose();

        var failureGroup = result.Diagnostics.Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error).Reverse().GroupBy(k =>
        {
            if (k.Location.SourceTree is null) return "";
            if (trees.TryGetValue(k.Location.SourceTree, out var path)) return path.Key;
            return "";
        }).ToDictionary(k => k.Key, k => k);

        trees.Clear();

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