using System;
using System.Collections.Generic;
using System.Runtime.Loader;
using StorybrewCommon.Scripting;

namespace StorybrewEditor.Scripting;

public class ScriptContainerContext<TScript>(string scriptTypeName, string mainSourcePath, string libraryFolder, string compiledScriptsPath, IEnumerable<string> referencedAssemblies) : ScriptContainerBase<TScript>(scriptTypeName, mainSourcePath, libraryFolder, compiledScriptsPath, referencedAssemblies) where TScript : Script
{
    AssemblyLoadContext appDomain;
    protected override ScriptProvider<TScript> LoadScript()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            AssemblyLoadContext scriptDomain = new($"{Name} {Id}", true);
            ScriptProvider<TScript> scriptProvider = new();

            try
            {
                scriptProvider.Initialize(ScriptCompiler.Compile(scriptDomain, SourcePaths, Name + Environment.TickCount64, ReferencedAssemblies), ScriptTypeName);
            }
            catch
            {
                UnloadDomain(scriptDomain);
                throw;
            }

            if (appDomain != null) UnloadDomain(scriptDomain);
            appDomain = scriptDomain;

            return scriptProvider;
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

    #region IDisposable Support

    bool disposed;
    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (appDomain is not null)
            {
                UnloadDomain(appDomain);
                appDomain = null;
            }
            disposed = true;
        }
        base.Dispose(disposing);
    }

    static void UnloadDomain(AssemblyLoadContext context)
    {
        context.Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    #endregion
}