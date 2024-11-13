﻿namespace StorybrewEditor.Scripting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using StorybrewCommon.Scripting;

public class ScriptContainer<TScript> : IDisposable where TScript : Script
{
    static int nextId;
    public readonly int Id = nextId++;

    AssemblyLoadContext appDomain;

    volatile int currentVersion, targetVersion = 1;

    List<string> referencedAssemblies = [];
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
            var newReferencedAssemblies = value as List<string> ?? value.ToList();
            if (newReferencedAssemblies.Count == referencedAssemblies.Count &&
                newReferencedAssemblies.All(referencedAssemblies.Contains)) return;

            referencedAssemblies = newReferencedAssemblies;
            ReloadScript();
        }
    }

    public bool HasScript => scriptType is not null || currentVersion != targetVersion;

    public event EventHandler OnScriptChanged;

    public TScript CreateScript()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var localTargetVersion = targetVersion;
        if (currentVersion < localTargetVersion)
        {
            currentVersion = localTargetVersion;
            try
            {
                AssemblyLoadContext scriptDomain = new(Name + Id, true);
                try
                {
                    scriptType = ScriptCompiler
                        .Compile(scriptDomain, SourcePaths, Name + Environment.TickCount, referencedAssemblies)
                        .GetType(ScriptTypeName, true);

                    appDomain?.Unload();
                    appDomain = scriptDomain;
                }
                catch
                {
                    scriptDomain.Unload();
                    throw;
                }
            }
            catch (ScriptCompilationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw CreateScriptLoadingException(e);
            }
        }

        var script = Unsafe.As<TScript>(Activator.CreateInstance(scriptType, true));
        script.Identifier = scriptType.AssemblyQualifiedName + Environment.TickCount;
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

    protected ScriptLoadingException CreateScriptLoadingException(Exception e)
    {
        var details = "";
        if (e is TypeLoadException) details = "Make sure the script's class name is the same as the file name.\n";
        return new ScriptLoadingException($"{ScriptTypeName} failed to load.\n{details}\n{e}");
    }

    #region IDisposable Support

    bool disposed;
    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;
        appDomain?.Unload();
        disposed = true;
    }
    public void Dispose() => Dispose(true);

    #endregion
}