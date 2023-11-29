using BrewLib.Util;
using StorybrewCommon.Scripting;
using System;
using System.Reflection;

namespace StorybrewEditor.Scripting
{
    public class ScriptProvider<TScript> where TScript : Script
    {
        Type type;

        public void Initialize(byte[] stream, string typeName) => type = Assembly.Load(stream).GetType(typeName, true);
        public TScript CreateScript()
        {
            var script = (TScript)Activator.CreateInstance(type, true);
            script.Identifier = type.AssemblyQualifiedName + Environment.TickCount64;
            return script;
        }
    }
}