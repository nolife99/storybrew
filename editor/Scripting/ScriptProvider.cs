using StorybrewCommon.Scripting;
using System;
using System.Runtime.Loader;

namespace StorybrewEditor.Scripting
{
    public class ScriptProvider<TScript> : IProvider<TScript> where TScript : Script
    {
        readonly string identifier = Guid.NewGuid().ToString();
        Type type;

        public void Initialize(string assemblyPath, string typeName)
        {
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            type = assembly.GetType(typeName, true, true);
        }
        public TScript CreateScript()
        {
            var script = (TScript)Activator.CreateInstance(type);
            script.Identifier = identifier;
            return script;
        }
    }
}