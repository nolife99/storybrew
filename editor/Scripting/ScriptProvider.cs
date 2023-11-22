using StorybrewCommon.Scripting;
using System;
using System.IO;
using System.Reflection;

namespace StorybrewEditor.Scripting
{
    public class ScriptProvider<TScript> : MarshalByRefObject where TScript : Script
    {
        readonly string identifier = Guid.NewGuid().ToString();
        Type type;

        public void Initialize(string assemblyPath, string typeName)
        {
            var assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
            File.Delete(assemblyPath);
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