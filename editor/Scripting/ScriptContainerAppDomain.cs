using BrewLib.Util;
using StorybrewCommon.Scripting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Security.Permissions;

namespace StorybrewEditor.Scripting
{
    public class ScriptContainerAppDomain<TScript> : ScriptContainerBase<TScript> where TScript : Script
    {
        AppDomain appDomain;

        public ScriptContainerAppDomain(string scriptTypeName, string mainSourcePath, string libraryFolder, string compiledScriptsPath, IEnumerable<string> referencedAssemblies)
            : base(scriptTypeName, mainSourcePath, libraryFolder, compiledScriptsPath, referencedAssemblies) { }

        protected override ScriptProvider<TScript> LoadScript()
        {
            if (disposed) return null;

            try
            {
                var assemblyPath = $"{CompiledScriptsPath}/{HashHelper.GetMd5(Name + Environment.TickCount)}.dll";
                ScriptCompiler.Compile(SourcePaths, assemblyPath, ReferencedAssemblies);

                var setup = new AppDomainSetup
                {
                    ApplicationName = $"{Name} {Id}",
                    ApplicationBase = AppDomain.CurrentDomain.SetupInformation.ApplicationBase,
                    DisallowCodeDownload = true,
                    DisallowPublisherPolicy = true,
                    DisallowBindingRedirects = true
                };

                Debug.Print($"{nameof(Scripting)}: Loading domain {setup.ApplicationName}");
                var scriptDomain = AppDomain.CreateDomain(setup.ApplicationName, null, setup, new PermissionSet(PermissionState.Unrestricted));

                ScriptProvider<TScript> scriptProvider;
                try
                {
                    scriptProvider = (ScriptProvider<TScript>)scriptDomain.CreateInstanceFromAndUnwrap(
                        typeof(ScriptProvider<TScript>).Assembly.ManifestModule.FullyQualifiedName,
                        typeof(ScriptProvider<TScript>).FullName);

                    scriptProvider.Initialize(assemblyPath, ScriptTypeName);
                }
                catch
                {
                    AppDomain.Unload(scriptDomain);
                    throw;
                }

                if (appDomain != null)
                {
                    Debug.Print($"{nameof(Scripting)}: Unloading domain {appDomain.FriendlyName}");
                    AppDomain.Unload(appDomain);
                }
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
                if (appDomain != null) AppDomain.Unload(appDomain);
                appDomain = null;

                disposed = true;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}