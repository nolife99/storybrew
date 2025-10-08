namespace StorybrewEditor.Scripting;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using BrewLib.IO;
using BrewLib.Util;
using SDL3;
using StorybrewCommon.Scripting;
using StorybrewEditor.Storyboarding;
using StorybrewEditor.Util;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;
using ZLinq;

public sealed class ScriptManager<TScript> : IDisposable where TScript : Script
{
    readonly FileSystemWatcher libraryWatcher;
    readonly ResourceContainer resourceContainer;
    readonly string scriptsNamespace, commonScriptsPath, scriptsLibraryPath;

    readonly FileSystemWatcher scriptWatcher;

    bool disposed;

    ValueList<string> referencedAssemblies = ValueList.Create<string>();
    ThrottledActionScheduler scheduler = new();

    ValueDictionary<string, ScriptContainer<TScript>> scriptContainers = ValueDictionary
        .Create<string, ScriptContainer<TScript>>();

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
            NotifyFilter = NotifyFilters.LastWrite
        };

        scriptWatcher.Created += scriptWatcher_Changed;
        scriptWatcher.Changed += scriptWatcher_Changed;
        scriptWatcher.Renamed += scriptWatcher_Changed;
        scriptWatcher.Deleted += scriptWatcher_Changed;
        scriptWatcher.Error += (_, e) => SDL.LogError(LogCategory.Test,
            $"Watcher error (script): {e.GetException()}");

        scriptWatcher.EnableRaisingEvents = true;
        SDL.LogInfo(LogCategory.Test, $"Watching (script): {scriptsSourcePath}");

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
        libraryWatcher.Error += (_, e) => SDL.LogError(LogCategory.Test,
            $"Watcher error (library): {e.GetException()}");

        libraryWatcher.EnableRaisingEvents = true;
        SDL.LogInfo(LogCategory.Test, $"Watching (library): {scriptsLibraryPath}");
    }

    public ReadOnlySpan<string> ReferencedAssemblies
    {
        get => referencedAssemblies.AsReadOnlySpan();
        set
        {
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

    public PooledArray<string> GetScriptNames()
        => Directory.EnumerateFiles(ScriptsPath, "*.cs", SearchOption.TopDirectoryOnly)
            .AsValueEnumerable()
            .Select(Path.GetFileNameWithoutExtension)
            .Union(Directory.EnumerateFiles(commonScriptsPath, "*.cs", SearchOption.TopDirectoryOnly)
                .AsValueEnumerable()
                .Select(Path.GetFileNameWithoutExtension))
            .ToArrayPool();

    void scriptWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        SDL.LogInfo(LogCategory.Test, $"Watched script file {e.ChangeType}: {e.FullPath}");

        if (e.ChangeType is not WatcherChangeTypes.Changed) scheduleSolutionUpdate();
        if (e.ChangeType is not WatcherChangeTypes.Deleted)
            scheduler?.Schedule(e.FullPath,
                _ =>
                {
                    if (!disposed && scriptContainers.GetAlternateLookup<ReadOnlySpan<char>>()
                        .TryGetValue(Path.GetFileNameWithoutExtension(e.Name.AsSpan()), out var container))
                        container.ReloadScript();
                });
    }

    void libraryWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        SDL.LogInfo(LogCategory.Test, $"Watched library file {e.ChangeType}: {e.FullPath}");

        if (e.ChangeType is not WatcherChangeTypes.Changed) scheduleSolutionUpdate();
        if (e.ChangeType is not WatcherChangeTypes.Deleted)
            scheduler?.Schedule(e.FullPath,
                _ =>
                {
                    if (disposed) return;

                    foreach (var container in scriptContainers.Values) container.ReloadScript();
                });
    }

    void scheduleSolutionUpdate()
        => scheduler?.Schedule($"*{nameof(updateSolutionFiles)}",
            _ =>
            {
                if (disposed) return;

                updateSolutionFiles();
            });

    void updateSolutionFiles()
    {
        SDL.LogInfo(LogCategory.Test, "Updating solution files");

        using (var slnStream = File.Create(Path.Combine(ScriptsPath, "storyboard.sln")))
        using (var resourceStream = resourceContainer.GetStream("project/storyboard.sln", ResourceSource.Embedded))
            resourceStream.CopyTo(slnStream, 65536);

        XmlDocument document = new() { PreserveWhitespace = false };
        try
        {
            using (var sr = XmlReader.Create(
                resourceContainer.GetStream("project/scripts.csproj", ResourceSource.Embedded),
                new() { CloseInput = true })) document.Load(sr);

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
            SDL.LogError(LogCategory.Test, $"Updating scripts.csproj: {e}");
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