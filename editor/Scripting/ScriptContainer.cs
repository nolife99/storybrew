namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using StorybrewCommon.Scripting;
using Tiny.PooledCollections.Generic;

public sealed class ScriptContainer<TScript> : IDisposable where TScript : Script
{
    static int nextId;
    public readonly int Id = nextId++;

    AssemblyLoadContext appDomain;

    volatile int currentVersion, targetVersion = 1;

    PooledList<string> referencedAssemblies;
    Type scriptType;

    public ScriptContainer(string scriptTypeName,
        string mainSourcePath,
        string libraryFolder,
        IEnumerable<string> referencedAssemblies)
    {
        ScriptTypeName = scriptTypeName;
        MainSourcePath = mainSourcePath;
        LibraryFolder = libraryFolder;

        ReferencedAssemblies = referencedAssemblies;
    }

    public string Name
    {
        get
        {
            var name = ScriptTypeName;
            if (name.Contains('.')) name = name[(name.LastIndexOf('.') + 1)..];
            return name;
        }
    }

    public string ScriptTypeName { get; }
    public string MainSourcePath { get; }
    public string LibraryFolder { get; }

    public IEnumerable<string> SourcePaths
    {
        get
        {
            if (LibraryFolder is null || !Directory.Exists(LibraryFolder)) return [MainSourcePath];

            return Directory.EnumerateFiles(LibraryFolder, "*.cs", SearchOption.AllDirectories).Union([MainSourcePath]);
        }
    }

    public IEnumerable<string> ReferencedAssemblies
    {
        get => referencedAssemblies;
        set
        {
            PooledList<string> newReferencedAssemblies = new(value.Distinct());
            if (referencedAssemblies is not null &&
                newReferencedAssemblies.Count == referencedAssemblies.Count &&
                newReferencedAssemblies.TrueForAll(referencedAssemblies.Contains)) return;

            referencedAssemblies?.Dispose();
            referencedAssemblies = newReferencedAssemblies;

            ReloadScript();
        }
    }

    public bool HasScript => scriptType is not null || currentVersion != targetVersion;

    public void Dispose()
    {
        appDomain?.Unload();
        referencedAssemblies?.Dispose();
    }

    public event EventHandler OnScriptChanged;

    public TScript CreateScript(CancellationTokenSource token)
    {
        var localTargetVersion = targetVersion;
        if (currentVersion < localTargetVersion)
        {
            currentVersion = localTargetVersion;
            AssemblyLoadContext scriptDomain = new(Name + Id, true);

            try
            {
                scriptType = ScriptCompiler.Compile(
                        scriptDomain,
                        SourcePaths,
                        Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture),
                        referencedAssemblies,
                        token)
                    .GetType(ScriptTypeName, true);

                appDomain?.Unload();
                appDomain = scriptDomain;
            }
            catch (Exception e)
            {
                scriptDomain.Unload();
                if (e is ScriptCompilationException or OperationCanceledException) throw;

                var details = "";
                if (e is TypeLoadException) details = "Make sure the script's class name is the same as the file name.\n";
                throw new ScriptLoadingException($"{ScriptTypeName} failed to load.\n{details}\n{e}");
            }
        }

        var script = (TScript)Activator.CreateInstance(scriptType!, true);
        script.Identifier = scriptType.AssemblyQualifiedName + Environment.CurrentManagedThreadId;
        return script;
    }

    public void ReloadScript()
    {
        var initialTargetVersion = targetVersion;

        int localCurrentVersion;
        do
        {
            localCurrentVersion = currentVersion;
            if (targetVersion <= localCurrentVersion) targetVersion = localCurrentVersion + 1;
        }
        while (currentVersion != localCurrentVersion);

        if (targetVersion > initialTargetVersion) OnScriptChanged?.Invoke(this, EventArgs.Empty);
    }
}