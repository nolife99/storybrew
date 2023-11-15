using BrewLib.Util;
using StorybrewCommon.Scripting;
using System;
using System.IO;
using System.Runtime.Loader;

namespace StorybrewEditor.Scripting
{
    public class ScriptProvider<TScript> : IProvider<TScript> where TScript : Script
    {
        Type type;

        public void Initialize(AssemblyLoadContext context, string assemblyPath, string typeName)
        {
            var stream = File.OpenRead(assemblyPath);
            var assembly = (context ?? AssemblyLoadContext.Default).LoadFromStream(stream);
            stream.Dispose();

            File.Delete(assemblyPath);
            type = assembly.GetType(typeName, true, true);
        }
        public TScript CreateScript()
        {
            var script = (TScript)Activator.CreateInstance(type);
            script.Identifier = HashHelper.GetMd5(type.AssemblyQualifiedName + Environment.TickCount64);
            return script;
        }
    }
}