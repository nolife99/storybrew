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

        protected override IProvider<TScript> LoadScript()
        {
            ObjectDisposedException.ThrowIf(disposed, GetType());
            try
            {
                var assemblyPath = $"{CompiledScriptsPath}/{Name + Environment.TickCount64}.dll";

                var id = $"{Name} {Id}";
                Trace.WriteLine($"{nameof(Scripting)}: Loading domain {id}");
                AssemblyLoadContext context = new(id, true);

                ScriptCompiler.Compile(context, SourcePaths, assemblyPath, ReferencedAssemblies);

                IProvider<TScript> scriptProvider;
                try
                {
                    scriptProvider = (IProvider<TScript>)context.LoadFromAssemblyPath(typeof(ScriptProvider<TScript>).Assembly.Location).CreateInstance(typeof(ScriptProvider<TScript>).FullName);
                    scriptProvider.Initialize(context, assemblyPath, ScriptTypeName);
                }
                catch
                {
                    context.Unload();
                    throw;
                }

                if (context is not null)
                {
                    Trace.WriteLine($"{nameof(Scripting)}: Unloading domain {id}");
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
                if (context is not null)
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