using StorybrewCommon.Scripting;
using System;
using System.Collections.Generic;

namespace StorybrewEditor.Scripting;

public class ScriptContainerContext<TScript>(string scriptTypeName, string mainSourcePath, string libraryFolder, string compiledScriptsPath, IEnumerable<string> referencedAssemblies) : ScriptContainerBase<TScript>(scriptTypeName, mainSourcePath, libraryFolder, compiledScriptsPath, referencedAssemblies) where TScript : Script
{
    protected override ScriptProvider<TScript> LoadScript()
    {
        /* This implementation is empty due to a replacement. (ScriptContainerAppDomain -> ScriptContainerContext)
           Currently we use AsyncLocal instead of AppDomain, which seems to work.
           However, compatibility hasn't been thoroughly tested. */ 
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