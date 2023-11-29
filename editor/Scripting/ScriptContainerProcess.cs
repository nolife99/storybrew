using StorybrewCommon.Scripting;
using StorybrewEditor.Processes;
using System;
using System.Collections.Generic;
using System.IO;

namespace StorybrewEditor.Scripting
{
    public class ScriptContainerProcess<TScript>(string scriptTypeName, string mainSourcePath, string libraryFolder, string compiledScriptsPath, IEnumerable<string> referencedAssemblies) : ScriptContainerBase<TScript>(scriptTypeName, mainSourcePath, libraryFolder, compiledScriptsPath, referencedAssemblies) where TScript : Script
    {
        RemoteProcessWorkerContainer workerProcess;

        protected override ScriptProvider<TScript> LoadScript()
        {
            ObjectDisposedException.ThrowIf(disposedValue, this);
            try
            {
                workerProcess?.Dispose();
                workerProcess = new();

                var scriptProvider = workerProcess.Worker.CreateScriptProvider<TScript>();
                scriptProvider.Initialize(ScriptCompiler.Compile(SourcePaths, Name + Environment.TickCount64, ReferencedAssemblies), ScriptTypeName);
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

        bool disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing) workerProcess?.Dispose();
                workerProcess = null;
                disposedValue = true;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}