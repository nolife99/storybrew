namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using BrewLib.IO;
using BrewLib.Util;
using Storyboarding;
using StorybrewCommon.Scripting;
using Tiny.PooledCollections.Generic;
using Tiny.PooledCollections.Generic.Internals;
using Util;

public sealed class ScriptManager<TScript> : IDisposable where TScript : Script
{
    readonly FileSystemWatcher libraryWatcher;
    readonly ResourceContainer resourceContainer;
    readonly PooledDictionary<string, ScriptContainer<TScript>> scriptContainers = new();
    readonly string scriptsNamespace, commonScriptsPath, scriptsLibraryPath;

    readonly FileSystemWatcher scriptWatcher;

    bool disposed;

    PooledList<string> referencedAssemblies = new();
    ThrottledActionScheduler scheduler = new();

    public ScriptManager(ResourceContainer resourceContainer,
        string scriptsNamespace,
        string scriptsSourcePath,
        string commonScriptsPath,
        string scriptsLibraryPath,
        ReadOnlySpan<string> referencedAssemblies)
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
            Filter = "*.cs",
            Path = scriptsLibraryPath,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite
        };

        libraryWatcher.Created += libraryWatcher_Changed;
        libraryWatcher.Changed += libraryWatcher_Changed;
        libraryWatcher.Renamed += libraryWatcher_Changed;
        libraryWatcher.Deleted += libraryWatcher_Changed;
        libraryWatcher.Error += (_, e) => Trace.WriteLine($"Watcher error (library): {e.GetException()}");
        libraryWatcher.EnableRaisingEvents = true;
        Trace.WriteLine($"Watching (library): {scriptsLibraryPath}");
    }

    public ReadOnlySpan<string> ReferencedAssemblies
    {
        get => referencedAssemblies.AsReadOnlySpan();
        set
        {
            referencedAssemblies ??= new();
            referencedAssemblies.Clear();
            referencedAssemblies.AddRange(value);

            foreach (var container in scriptContainers.Values) container.ReferencedAssemblies = value;

            updateSolutionFiles();
        }
    }

    public string ScriptsPath { get; }
    public void Dispose() => Dispose(true);

    public ScriptContainer<TScript> Get(ReadOnlySpan<char> scriptName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var altLookup = scriptContainers.GetAlternateLookup<ReadOnlySpan<char>>();
        if (altLookup.TryGetValue(scriptName, out var scriptContainer)) return scriptContainer;

        var scriptFileName = $"{scriptName}.cs";
        var sourcePath = Path.Combine(ScriptsPath, scriptFileName);

        if (commonScriptsPath is not null && !File.Exists(sourcePath))
        {
            var commonSourcePath = Path.Combine(commonScriptsPath, scriptFileName);
            if (File.Exists(commonSourcePath))
            {
                File.Copy(commonSourcePath, sourcePath);
                File.SetAttributes(sourcePath, File.GetAttributes(sourcePath) & ~FileAttributes.ReadOnly);
            }
        }

        return altLookup[scriptName] = new($"{scriptsNamespace}.{scriptName}",
            sourcePath,
            scriptsLibraryPath,
            referencedAssemblies.AsReadOnlySpan());
    }

    public IEnumerable<string> GetScriptNames() => Directory
        .EnumerateFiles(ScriptsPath, "*.cs", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileNameWithoutExtension)
        .Union(Directory.EnumerateFiles(commonScriptsPath, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension));

    void scriptWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        Trace.WriteLine($"Watched script file {e.ChangeType}: {e.FullPath}");

        if (e.ChangeType is not WatcherChangeTypes.Changed) scheduleSolutionUpdate();
        if (e.ChangeType is not WatcherChangeTypes.Deleted)
            scheduler?.Schedule(e.FullPath,
                _ =>
                {
                    if (!disposed &&
                        scriptContainers.TryGetValue(Path.GetFileNameWithoutExtension(e.Name), out var container))
                        container.ReloadScript();
                });
    }

    void libraryWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        Trace.WriteLine($"Watched library file {e.ChangeType}: {e.FullPath}");

        if (e.ChangeType is not WatcherChangeTypes.Changed) scheduleSolutionUpdate();
        if (e.ChangeType is not WatcherChangeTypes.Deleted)
            scheduler?.Schedule(e.FullPath,
                _ =>
                {
                    if (disposed) return;

                    foreach (var container in scriptContainers.Values) container.ReloadScript();
                });
    }

    void scheduleSolutionUpdate() => scheduler?.Schedule($"*{nameof(updateSolutionFiles)}",
        _ =>
        {
            if (disposed) return;

            updateSolutionFiles();
        });

    void updateSolutionFiles()
    {
        Trace.WriteLine("Updating solution files");

        using (var slnStream = File.Create(Path.Combine(ScriptsPath, "storyboard.sln")))
        using (var resourceStream = resourceContainer.GetStream("project/storyboard.sln", ResourceSource.Embedded))
            resourceStream.CopyTo(slnStream, 65536);

        XmlDocument document = new() { PreserveWhitespace = false };
        try
        {
            using (var stream = resourceContainer.GetStream("project/scripts.csproj", ResourceSource.Embedded))
            using (XmlTextReader sr = new(stream))
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

        foreach (var container in scriptContainers.Values) container.Dispose();
        scriptContainers.Dispose();

        referencedAssemblies.Dispose();

        scheduler = null;
        disposed = true;
    }
}