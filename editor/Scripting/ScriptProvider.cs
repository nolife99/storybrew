using System;
using System.Reflection;
using StorybrewCommon.Scripting;

namespace StorybrewEditor.Scripting;

public class ScriptProvider<TScript> where TScript : Script
{
    Type type;

    public void Initialize(Assembly assembly, string typeName) => type = assembly.GetType(typeName, true);
    public TScript CreateScript()
    {
        var script = (TScript)Activator.CreateInstance(type, true);
        script.Identifier = type.AssemblyQualifiedName + Environment.CurrentManagedThreadId;
        return script;
    }
}