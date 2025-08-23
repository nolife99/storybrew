namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Threading;
using StorybrewCommon.Scripting;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;
using ZLinq;

public sealed class ScriptContainer<TScript> : IDisposable where TScript : Script
{
    AssemblyLoadContext appDomain;

    volatile int currentVersion, targetVersion = 1;

    ValueList<string> referencedAssemblies;
    Type scriptType;

    public ScriptContainer(string scriptTypeName,
        string mainSourcePath,
        string libraryFolder,
        ReadOnlySpan<string> referencedAssemblies)
    {
        ScriptTypeName = scriptTypeName;
        MainSourcePath = mainSourcePath;
        LibraryFolder = libraryFolder;

        ReferencedAssemblies = referencedAssemblies;
    }

    public ReadOnlySpan<char> Name
    {
        get
        {
            var name = ScriptTypeName.AsSpan();
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

    public ReadOnlySpan<string> ReferencedAssemblies
    {
        get => referencedAssemblies.AsReadOnlySpan();
        set
        {
            using var distinct = value.AsValueEnumerable().Distinct().ToArrayPool();
            if (distinct.Size == referencedAssemblies.Count)
            {
                var areSame = true;
                foreach (var ass in distinct.Span)
                    if (!referencedAssemblies.Contains(ass))
                    {
                        areSame = false;
                        break;
                    }

                if (areSame) return;
            }

            if (referencedAssemblies.IsValid) referencedAssemblies.Clear();
            else referencedAssemblies = ValueList.Create<string>();

            referencedAssemblies.AddRange(distinct.Span);

            ReloadScript();
        }
    }

    public bool HasScript => scriptType is not null || currentVersion != targetVersion;

    public void Dispose()
    {
        appDomain?.Unload();
        referencedAssemblies.Dispose();

        scriptType = null;
    }

    public event EventHandler OnScriptChanged;

    public TScript CreateScript(CancellationTokenSource token)
    {
        var localTargetVersion = targetVersion;
        var localCurrentVersion = currentVersion;

        if (currentVersion < localTargetVersion)
        {
            currentVersion = localTargetVersion;
            AssemblyLoadContext scriptDomain = new(null, true);

            try
            {
                scriptType = ScriptCompiler
                    .Compile(scriptDomain, SourcePaths, referencedAssemblies.AsReadOnlySpan(), token)
                    .GetModules()
                    .AsValueEnumerable()
                    .SelectMany(m => m.GetTypes())
                    .First(t => t.IsAssignableTo(typeof(TScript)) && !t.IsAbstract);
            }
            catch (Exception e)
            {
                scriptDomain.Unload();

                string details = null;
                switch (e)
                {
                    case ScriptCompilationException: throw;

                    case OperationCanceledException:
                        currentVersion = localCurrentVersion;
                        throw;

                    case InvalidOperationException:
                        details = "Make sure the script is not abstract and inherits from StoryboardObjectGenerator.\n";
                        break;
                }

                throw new ScriptLoadingException($"{ScriptTypeName} failed to load.\n{details}\n{e}");
            }

            appDomain?.Unload();
            appDomain = scriptDomain;
        }

        var script = (TScript)Activator.CreateInstance(scriptType!, true);
        script!.Identifier = Environment.TickCount;
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