﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using StorybrewCommon.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace StorybrewEditor.Scripting
{
    public class ScriptCompiler : MarshalByRefObject
    {
        static readonly string[] environmentDirectories =
        {
            Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "WPF"),
            Path.GetDirectoryName(typeof(object).Assembly.Location),
            Environment.CurrentDirectory
        };
        static int nextId;

        public static void Compile(string[] sourcePaths, string outputPath, IEnumerable<string> referencedAssemblies)
        {
            var setup = new AppDomainSetup
            {
                ApplicationName = $"ScriptCompiler {nextId++}",
                ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase
            };

            Debug.Print($"{nameof(Scripting)}: Compiling {string.Join(", ", sourcePaths)}");
            var compilerDomain = AppDomain.CreateDomain(setup.ApplicationName, null, setup);
            try
            {
                var compiler = (ScriptCompiler)compilerDomain.CreateInstanceFromAndUnwrap(
                    typeof(ScriptCompiler).Assembly.ManifestModule.FullyQualifiedName,
                    typeof(ScriptCompiler).FullName);

                compile(sourcePaths, outputPath, referencedAssemblies);
            }
            finally
            {
                AppDomain.Unload(compilerDomain);
            }
        }

        static void compile(string[] sourcePaths, string outputPath, IEnumerable<string> referencedAssemblies)
        {
            var trees = new Dictionary<SyntaxTree, KeyValuePair<string, SourceText>>();
            for (var i = 0; i < sourcePaths.Length; ++i) using (var sourceStream = File.OpenRead(sourcePaths[i]))
            {
                var sourceText = SourceText.From(sourceStream, canBeEmbedded: true);
                var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp7_3);

                var syntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, parseOptions);
                trees[syntaxTree] = new KeyValuePair<string, SourceText>(sourcePaths[i], sourceText);
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
                    message.AppendLine($"{Path.GetFileName(file)}:");
                    foreach (var diagnostic in diagnostics) message.AppendLine($"--{diagnostic}");
                }

                throw new ScriptCompilationException(message.ToString());
            }
        }
    }
}