using BrewLib.Util;
using StorybrewCommon.Scripting;
using System;
using System.IO;
using System.Reflection;

namespace StorybrewEditor.Scripting
{
    public class ScriptProvider<TScript> where TScript : Script
    {
        Type type;

        public void Initialize(string assemblyPath, string typeName)
        {
            var assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
            if (File.Exists(assemblyPath)) File.Delete(assemblyPath);
            type = assembly.GetType(typeName, true);
        }
        public TScript CreateScript()
        {
            var script = (TScript)Activator.CreateInstance(type);
            script.Identifier = HashHelper.GetMd5(type.AssemblyQualifiedName + Environment.TickCount64);
            return script;
        }
    }
}