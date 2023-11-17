using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace StorybrewEditor.Scripting
{
    public class ScriptCompiler : MarshalByRefObject
    {
        static readonly string[] environmentDirectories =
        [
            Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "WPF"),
            Path.GetDirectoryName(typeof(object).Assembly.Location),
            Path.GetDirectoryName(typeof(Brush).Assembly.Location),
            Environment.CurrentDirectory
        ];
        static readonly int nextId;

        public static void Compile(IEnumerable<string> sourcePaths, string outputPath, IEnumerable<string> referencedAssemblies)
        {
            Debug.Print($"{nameof(Scripting)}: Compiling {string.Join(", ", sourcePaths)}");
            compile(sourcePaths, outputPath, referencedAssemblies);
        }

        static void compile(IEnumerable<string> sourcePaths, string outputPath, IEnumerable<string> referencedAssemblies)
        {
            Dictionary<SyntaxTree, KeyValuePair<string, SourceText>> trees = [];
            foreach (var src in sourcePaths) using (var sourceStream = File.OpenRead(src))
            {
                var sourceText = SourceText.From(sourceStream, canBeEmbedded: true);
                trees[SyntaxFactory.ParseSyntaxTree(sourceText, new CSharpParseOptions(LanguageVersion.Latest))] = new(src, sourceText);
            }
            var references = new HashSet<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            };

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
                    var message = new StringBuilder("Compilation error\n\n");
                    message.AppendLine(e.ToString());
                    throw new ScriptCompilationException(message.ToString());
                }
            }

            var compilation = CSharpCompilation.Create(Path.GetFileName(outputPath), trees.Keys, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    allowUnsafe: true, platform: Environment.Is64BitOperatingSystem ? Platform.X64 : Platform.AnyCpu32BitPreferred, optimizationLevel: 
                    OptimizationLevel.Debug, assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));

            using (var assemblyStream = File.Create(outputPath))
            {
                var result = compilation.Emit(assemblyStream, 
                    embeddedTexts: trees.Values.Select(k => EmbeddedText.FromSource(k.Key, k.Value)), 
                    options: new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded));

                if (result.Success)
                {
                    trees.Clear();
                    return;
                }

                var failureGroup = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity is DiagnosticSeverity.Error)
                    .Reverse().GroupBy(k =>
                {
                    if (k.Location.SourceTree is null) return "";
                    if (trees.TryGetValue(k.Location.SourceTree, out var path)) return path.Key;
                    return "";
                }).ToDictionary(k => k.Key, k => k);

                trees.Clear();

                var message = new StringBuilder("Compilation error\n\n");
                foreach (var kvp in failureGroup)
                {
                    var file = kvp.Key;
                    var diagnostics = kvp.Value;
                    message.AppendLine(CultureInfo.InvariantCulture, $"{Path.GetFileName(file)}:");
                    foreach (var diagnostic in diagnostics) message.AppendLine(CultureInfo.InvariantCulture, $"--{diagnostic}");
                }

                throw new ScriptCompilationException(message.ToString());
            }
        }
    }
}