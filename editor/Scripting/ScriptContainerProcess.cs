using StorybrewCommon.Scripting;
using StorybrewEditor.Processes;
using System;
using System.Collections.Generic;

namespace StorybrewEditor.Scripting;

public class ScriptContainerProcess<TScript>(string scriptTypeName, string mainSourcePath, string libraryFolder, string compiledScriptsPath, IEnumerable<string> referencedAssemblies) : ScriptContainerBase<TScript>(scriptTypeName, mainSourcePath, libraryFolder, compiledScriptsPath, referencedAssemblies) where TScript : Script
{
    RemoteProcessWorkerContainer workerProcess;

    protected override ScriptProvider<TScript> LoadScript()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            workerProcess?.Dispose();
            workerProcess = new();

            var scriptProvider = workerProcess.Worker.CreateScriptProvider<TScript>();
            scriptProvider.Initialize(ScriptCompiler.Compile(null, SourcePaths, Name + Environment.TickCount64, ReferencedAssemblies), ScriptTypeName);
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
            if (disposing) workerProcess?.Dispose();
            workerProcess = null;
            disposed = true;
        }
        base.Dispose(disposing);
    }

    #endregion
}