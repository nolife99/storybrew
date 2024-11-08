namespace StorybrewEditor.Scripting;

using System;

public class ScriptLoadingException : Exception
{
    public ScriptLoadingException() { }
    public ScriptLoadingException(string message) : base(message) { }
    public ScriptLoadingException(string message, Exception innerException) : base(message, innerException) { }
}