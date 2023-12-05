using BrewLib.Data;
using BrewLib.Util;
using StorybrewCommon.Scripting;
using StorybrewEditor.Storyboarding;
using StorybrewEditor.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace StorybrewEditor.Scripting;

public sealed class ScriptManager<TScript> : IDisposable where TScript : Script
{
    readonly ResourceContainer resourceContainer;
    readonly string scriptsNamespace, commonScriptsPath, scriptsLibraryPath, compiledScriptsPath;

    List<string> referencedAssemblies = [];
    public IEnumerable<string> ReferencedAssemblies
    {
        get => referencedAssemblies;
        set
        {
            referencedAssemblies = (value as List<string>) ?? value.ToList();
            foreach (var scriptContainer in scriptContainers.Values) scriptContainer.ReferencedAssemblies = referencedAssemblies;
            updateSolutionFiles();
        }
    }

    FileSystemWatcher scriptWatcher;
    readonly FileSystemWatcher libraryWatcher;
    ThrottledActionScheduler scheduler = new();
    readonly Dictionary<string, ScriptContainer<TScript>> scriptContainers = [];

    public string ScriptsPath { get; }

    public ScriptManager(ResourceContainer resourceContainer, string scriptsNamespace, string scriptsSourcePath, string commonScriptsPath, string scriptsLibraryPath, string compiledScriptsPath, IEnumerable<string> referencedAssemblies)
    {
        this.resourceContainer = resourceContainer;
        this.scriptsNamespace = scriptsNamespace;
        ScriptsPath = scriptsSourcePath;
        this.commonScriptsPath = commonScriptsPath;
        this.scriptsLibraryPath = scriptsLibraryPath;
        this.compiledScriptsPath = compiledScriptsPath;

        ReferencedAssemblies = referencedAssemblies;

        scriptWatcher = new()
        {
            Filter = "*.cs",
            Path = scriptsSourcePath,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite
        };

        scriptWatcher.Created += scriptWatcher_Changed;
        scriptWatcher.Changed += scriptWatcher_Changed;
        scriptWatcher.Renamed += scriptWatcher_Changed;
        scriptWatcher.Deleted += scriptWatcher_Changed;
        scriptWatcher.Error += (sender, e) => Trace.WriteLine($"Watcher error (script): {e.GetException()}");
        scriptWatcher.EnableRaisingEvents = true;
        Trace.WriteLine($"Watching (script): {scriptsSourcePath}");

        libraryWatcher = new()
        {
            Filter = "*.cs",
            Path = scriptsLibraryPath,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite
        };

        libraryWatcher.Created += libraryWatcher_Changed;
        libraryWatcher.Changed += libraryWatcher_Changed;
        libraryWatcher.Renamed += libraryWatcher_Changed;
        libraryWatcher.Deleted += libraryWatcher_Changed;
        libraryWatcher.Error += (sender, e) => Trace.WriteLine($"Watcher error (library): {e.GetException()}");
        libraryWatcher.EnableRaisingEvents = true;
        Trace.WriteLine($"Watching (library): {scriptsLibraryPath}");
    }

    public ScriptContainer<TScript> Get(string scriptName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (scriptContainers.TryGetValue(scriptName, out var scriptContainer)) return scriptContainer;

        var scriptTypeName = $"{scriptsNamespace}.{scriptName}";
        var sourcePath = Path.Combine(ScriptsPath, $"{scriptName}.cs");

        if (commonScriptsPath is not null && !File.Exists(sourcePath))
        {
            var commonSourcePath = Path.Combine(commonScriptsPath, $"{scriptName}.cs");
            if (File.Exists(commonSourcePath))
            {
                File.Copy(commonSourcePath, sourcePath);
                File.SetAttributes(sourcePath, File.GetAttributes(sourcePath) & ~FileAttributes.ReadOnly);
            }
        }

        scriptContainers[scriptName] = scriptContainer = new ScriptContainerContext<TScript>(scriptTypeName, sourcePath, scriptsLibraryPath, compiledScriptsPath, referencedAssemblies);
        return scriptContainer;
    }
    public IEnumerable<string> GetScriptNames()
    {
        var projectScriptNames = new List<string>();
        foreach (var scriptPath in Directory.GetFiles(ScriptsPath, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(scriptPath);
            projectScriptNames.Add(name);
            yield return name;
        }
        foreach (var scriptPath in Directory.GetFiles(commonScriptsPath, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(scriptPath);
            if (!projectScriptNames.Contains(name)) yield return name;
        }
    }
    void scriptWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        var change = e.ChangeType.ToString().ToLowerInvariant();
        Trace.WriteLine($"Watched script file {change}: {e.FullPath}");

        if (e.ChangeType != WatcherChangeTypes.Changed) scheduleSolutionUpdate();
        if (e.ChangeType != WatcherChangeTypes.Deleted) scheduler?.Schedule(e.FullPath, _ =>
        {
            if (disposed) return;
            var scriptName = Path.GetFileNameWithoutExtension(e.Name);

            if (scriptContainers.TryGetValue(scriptName, out ScriptContainer<TScript> container)) container.ReloadScript();
        });
    }
    void libraryWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        var change = e.ChangeType.ToString().ToLowerInvariant();
        Trace.WriteLine($"Watched library file {change}: {e.FullPath}");

        if (e.ChangeType != WatcherChangeTypes.Changed) scheduleSolutionUpdate();
        if (e.ChangeType != WatcherChangeTypes.Deleted) scheduler?.Schedule(e.FullPath, key =>
        {
            if (disposed) return;
            foreach (var container in scriptContainers.Values) container.ReloadScript();
        });
    }
    void scheduleSolutionUpdate() => scheduler?.Schedule($"*{nameof(updateSolutionFiles)}", key =>
    {
        if (disposed) return;
        updateSolutionFiles();
    });
    void updateSolutionFiles()
    {
        Trace.WriteLine($"Updating solution files");

        var slnPath = Path.Combine(ScriptsPath, "storyboard.sln");
        File.WriteAllBytes(slnPath, resourceContainer.GetBytes("project/storyboard.sln", ResourceSource.Embedded | ResourceSource.Relative));

        var csProjPath = Path.Combine(ScriptsPath, "scripts.csproj");
        var document = new XmlDocument() { PreserveWhitespace = false };
        try
        {
            using (var stream = resourceContainer.GetStream("project/scripts.csproj", ResourceSource.Embedded | ResourceSource.Relative))
            using (var sr = new XmlTextReader(stream) { DtdProcessing = default, XmlResolver = null }) document.Load(sr);

            var xmlns = document.DocumentElement.GetAttribute("xmlns");

            var referencedAssembliesGroup = document.CreateElement("ItemGroup", xmlns);
            document.DocumentElement.AppendChild(referencedAssembliesGroup);
            foreach (var path in referencedAssemblies) if (!Project.DefaultAssemblies.Contains(path))
            {
                var isSystem = path.StartsWith("System.", StringComparison.Ordinal);
                var relativePath = isSystem ? path : PathHelper.GetRelativePath(ScriptsPath, path);

                var compileNode = document.CreateElement("Reference", xmlns);
                compileNode.SetAttribute("Include", isSystem ? path : AssemblyName.GetAssemblyName(path).Name);
                if (!isSystem)
                { 
                    var hintPath = document.CreateElement("HintPath", xmlns);
                    hintPath.AppendChild(document.CreateTextNode(relativePath));
                    compileNode.AppendChild(hintPath);
                }
                referencedAssembliesGroup.AppendChild(compileNode);
            }
            document.Save(csProjPath);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Failed to update scripts.csproj: {e}");
        }
    }

    bool disposed;
    public void Dispose()
    {
        if (!disposed)
        {
            scriptWatcher.Dispose();
            libraryWatcher.Dispose();
            scriptContainers.Dispose();

            scheduler = null;
            scriptWatcher = null;

            disposed = true;
        }
    }
}