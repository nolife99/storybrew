using BrewLib.Util;
using StorybrewCommon.Scripting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Loader;

namespace StorybrewEditor.Scripting
{
    public class ScriptContainerAppDomain<TScript> : ScriptContainerBase<TScript> where TScript : Script
    {
        AssemblyLoadContext context;
        string assemblyPath;

        public ScriptContainerAppDomain(string scriptTypeName, string mainSourcePath, string libraryFolder, string compiledScriptsPath, IEnumerable<string> referencedAssemblies)
            : base(scriptTypeName, mainSourcePath, libraryFolder, compiledScriptsPath, referencedAssemblies) { }

        protected override IProvider<TScript> LoadScript()
        {
            if (disposed) return null;

            try
            {
                assemblyPath = $"{CompiledScriptsPath}/{HashHelper.GetMd5(Name + Environment.TickCount64)}.dll";
                ScriptCompiler.Compile(SourcePaths, assemblyPath, ReferencedAssemblies);

                Trace.WriteLine($"{nameof(Scripting)}: Loading domain {$"{Name} {Id}"}");
                var scriptContext = new AssemblyLoadContext($"{Name} {Id}", true);

                IProvider<TScript> scriptProvider;
                try
                {
                    var assembly = scriptContext.LoadFromAssemblyName(typeof(ScriptProvider<TScript>).Assembly.GetName());
                    scriptProvider = (IProvider<TScript>)Activator.CreateInstance(assembly.GetType(typeof(ScriptProvider<TScript>).FullName));
                    scriptProvider.Initialize(scriptContext, assemblyPath, ScriptTypeName);
                }
                catch
                {
                    scriptContext.Unload();
                    throw;
                }

                if (context != null)
                {
                    Debug.Print($"{nameof(Scripting)}: Unloading domain {$"{Name} {Id}"}");
                    scriptContext.Unload();
                }
                context = scriptContext;

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