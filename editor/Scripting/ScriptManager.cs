namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using BrewLib.Data;
using BrewLib.Util;
using Storyboarding;
using StorybrewCommon.Scripting;
using Util;

public sealed class ScriptManager<TScript> : IDisposable where TScript : Script
{
    readonly FileSystemWatcher libraryWatcher;
    readonly ResourceContainer resourceContainer;
    readonly Dictionary<string, ScriptContainer<TScript>> scriptContainers = [];
    readonly string scriptsNamespace, commonScriptsPath, scriptsLibraryPath;

    readonly FileSystemWatcher scriptWatcher;

    bool disposed;

    List<string> referencedAssemblies = [];
    ThrottledActionScheduler scheduler = new();

    public ScriptManager(ResourceContainer resourceContainer,
        string scriptsNamespace,
        string scriptsSourcePath,
        string commonScriptsPath,
        string scriptsLibraryPath,
        IEnumerable<string> referencedAssemblies)
    {
        this.resourceContainer = resourceContainer;
        this.scriptsNamespace = scriptsNamespace;
        ScriptsPath = scriptsSourcePath;
        this.commonScriptsPath = commonScriptsPath;
        this.scriptsLibraryPath = scriptsLibraryPath;

        ReferencedAssemblies = referencedAssemblies;

        scriptWatcher = new()
        {
            Filter = "*.cs",
            Path = scriptsSourcePath,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite,
            InternalBufferSize = 16384
        };

        scriptWatcher.Created += scriptWatcher_Changed;
        scriptWatcher.Changed += scriptWatcher_Changed;
        scriptWatcher.Renamed += scriptWatcher_Changed;
        scriptWatcher.Deleted += scriptWatcher_Changed;
        scriptWatcher.Error += (_, e) => Trace.TraceError($"Watcher error (script): {e.GetException()}");
        scriptWatcher.EnableRaisingEvents = true;
        Trace.WriteLine($"Watching (script): {scriptsSourcePath}");

        libraryWatcher = new()
        {
            Filter = "*.cs", Path = scriptsLibraryPath, IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite
        };

        libraryWatcher.Created += libraryWatcher_Changed;
        libraryWatcher.Changed += libraryWatcher_Changed;
        libraryWatcher.Renamed += libraryWatcher_Changed;
        libraryWatcher.Deleted += libraryWatcher_Changed;
        libraryWatcher.Error += (_, e) => Trace.WriteLine($"Watcher error (library): {e.GetException()}");
        libraryWatcher.EnableRaisingEvents = true;
        Trace.WriteLine($"Watching (library): {scriptsLibraryPath}");
    }

    public IEnumerable<string> ReferencedAssemblies
    {
        get => referencedAssemblies;
        set
        {
            referencedAssemblies = value as List<string> ?? value.ToList();
            foreach (var container in scriptContainers.Values) container.ReferencedAssemblies = referencedAssemblies;
            updateSolutionFiles();
        }
    }

    public string ScriptsPath { get; }
    public void Dispose() => Dispose(true);

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

        scriptContainers[scriptName] = scriptContainer = new ScriptContainer<TScript>(scriptTypeName, sourcePath,
            scriptsLibraryPath, referencedAssemblies);

        return scriptContainer;
    }

    public IEnumerable<string> GetScriptNames()
    {
        HashSet<string> projectScriptNames = [];
        foreach (var scriptPath in Directory.EnumerateFiles(ScriptsPath, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(scriptPath);
            projectScriptNames.Add(name);
            yield return name;
        }

        foreach (var scriptPath in Directory.EnumerateFiles(commonScriptsPath, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(scriptPath);
            if (!projectScriptNames.Contains(name)) yield return name;
        }
    }

    void scriptWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        Trace.WriteLine($"Watched script file {e.ChangeType}: {e.FullPath}");

        if (e.ChangeType is not WatcherChangeTypes.Changed) scheduleSolutionUpdate();
        if (e.ChangeType is not WatcherChangeTypes.Deleted)
            scheduler?.Schedule(e.FullPath, _ =>
            {
                if (!disposed && scriptContainers.TryGetValue(Path.GetFileNameWithoutExtension(e.Name), out var container))
                    container.ReloadScript();
            });
    }

    void libraryWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        Trace.WriteLine($"Watched library file {e.ChangeType}: {e.FullPath}");

        if (e.ChangeType is not WatcherChangeTypes.Changed) scheduleSolutionUpdate();
        if (e.ChangeType is not WatcherChangeTypes.Deleted)
            scheduler?.Schedule(e.FullPath, _ =>
            {
                if (disposed) return;
                foreach (var container in scriptContainers.Values) container.ReloadScript();
            });
    }

    void scheduleSolutionUpdate() => scheduler?.Schedule($"*{nameof(updateSolutionFiles)}", _ =>
    {
        if (disposed) return;
        updateSolutionFiles();
    });

    void updateSolutionFiles()
    {
        Trace.WriteLine("Updating solution files");

        using (var slnStream = File.Create(Path.Combine(ScriptsPath, "storyboard.sln")))
        using (var resourceStream =
            resourceContainer.GetStream("project/storyboard.sln", ResourceSource.Embedded | ResourceSource.Relative))
            resourceStream.CopyTo(slnStream);

        XmlDocument document = new() { PreserveWhitespace = false };
        try
        {
            using (var stream =
                resourceContainer.GetStream("project/scripts.csproj", ResourceSource.Embedded | ResourceSource.Relative))
            using (XmlTextReader sr = new(stream) { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })
                document.Load(sr);

            var xmlns = document.DocumentElement.GetAttribute("xmlns");

            var referencedAssembliesGroup = document.CreateElement("ItemGroup", xmlns);
            document.DocumentElement.AppendChild(referencedAssembliesGroup);

            foreach (var path in referencedAssemblies)
            {
                if (Project.DefaultAssemblies.Contains(path)) continue;
                var compileNode = document.CreateElement("Reference", xmlns);
                compileNode.SetAttribute("Include", AssemblyName.GetAssemblyName(path).Name);

                var hintPath = document.CreateElement("HintPath", xmlns);
                hintPath.AppendChild(document.CreateTextNode(PathHelper.GetRelativePath(ScriptsPath, path)));
                compileNode.AppendChild(hintPath);
                referencedAssembliesGroup.AppendChild(compileNode);
            }

            using var csProjPath = File.Create(Path.Combine(ScriptsPath, "scripts.csproj"));
            document.Save(csProjPath);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Updating scripts.csproj: {e}");
        }
    }

    void Dispose(bool disposing)
    {
        if (disposed) return;
        scriptWatcher.Dispose();
        libraryWatcher.Dispose();

        if (!disposing) return;
        scriptContainers.Dispose();

        scheduler = null;
        disposed = true;
    }
}