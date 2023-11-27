using StorybrewCommon.Scripting;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace StorybrewEditor.Scripting
{
    public class ScriptContainerContext<TScript>(string scriptTypeName, string mainSourcePath, string libraryFolder, string compiledScriptsPath, IEnumerable<string> referencedAssemblies) : ScriptContainerBase<TScript>(scriptTypeName, mainSourcePath, libraryFolder, compiledScriptsPath, referencedAssemblies) where TScript : Script
    {
        protected override ScriptProvider<TScript> LoadScript()
        {
            ObjectDisposedException.ThrowIf(disposed, GetType());
            try
            {
                var assemblyPath = $"{CompiledScriptsPath}/{Name + Environment.TickCount64}.dll";

                var id = $"{Name} {Id}";
                Trace.WriteLine($"{nameof(Scripting)}: Loading domain {id}");
                ScriptCompiler.Compile(SourcePaths, assemblyPath, ReferencedAssemblies);

                ScriptProvider<TScript> scriptProvider = new();
                scriptProvider.Initialize(assemblyPath, ScriptTypeName);

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
                GC.Collect();
                GC.WaitForPendingFinalizers();

                disposed = true;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}