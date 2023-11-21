using BrewLib.Util;
using StorybrewCommon.Scripting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Loader;

namespace StorybrewEditor.Scripting
{
    public class ScriptContainerAppDomain<TScript>(string scriptTypeName, string mainSourcePath, string libraryFolder, string compiledScriptsPath, IEnumerable<string> referencedAssemblies) : ScriptContainerBase<TScript>(scriptTypeName, mainSourcePath, libraryFolder, compiledScriptsPath, referencedAssemblies) where TScript : Script
    {
        AssemblyLoadContext context;
        string assemblyPath;

        protected override IProvider<TScript> LoadScript()
        {
            if (disposed) return null;

            try
            {
                assemblyPath = $"{CompiledScriptsPath}/{HashHelper.GetMd5(Name + Environment.TickCount64)}.dll";
                ScriptCompiler.Compile(SourcePaths, assemblyPath, ReferencedAssemblies);

                Trace.WriteLine($"{nameof(Scripting)}: Loading domain {$"{Name} {Id}"}");
                var context = new AssemblyLoadContext($"{Name} {Id}", true);

                IProvider<TScript> scriptProvider;
                try
                {
                    scriptProvider = (IProvider<TScript>)Activator.CreateInstance(context.LoadFromAssemblyName(typeof(ScriptProvider<TScript>).Assembly.GetName())
                        .GetType(typeof(ScriptProvider<TScript>).FullName));
                    scriptProvider.Initialize(context, assemblyPath, ScriptTypeName);
                }
                catch
                {
                    context.Unload();
                    throw;
                }

                if (context != null)
                {
                    Debug.Print($"{nameof(Scripting)}: Unloading domain {$"{Name} {Id}"}");
                    context.Unload();
                }
                this.context = context;

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
                if (context != null)
                {
                    context.Unload();
                    context = null;
                }
                disposed = true;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}