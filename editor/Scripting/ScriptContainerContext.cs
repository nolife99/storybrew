using StorybrewCommon.Scripting;
using System;
using System.Collections.Generic;

namespace StorybrewEditor.Scripting
{
    public class ScriptContainerContext<TScript>(string scriptTypeName, string mainSourcePath, string libraryFolder, string compiledScriptsPath, IEnumerable<string> referencedAssemblies) : ScriptContainerBase<TScript>(scriptTypeName, mainSourcePath, libraryFolder, compiledScriptsPath, referencedAssemblies) where TScript : Script
    {
        protected override ScriptProvider<TScript> LoadScript()
        {
            /* This implementation is empty due to a replacement for AppDomain (thread-local), but this is not a fix-all solution.
               For example, scripts cannot invoke parallel actions or work with threads and asynchronous code.
               Sadly, AssemblyLoadContext is extremely painful to get working with transitive assemblies,
               as they don't provide the same amount of isolation that AppDomain originally gave. */ 
            try
            {
                ScriptProvider<TScript> scriptProvider = new();
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
    }
}