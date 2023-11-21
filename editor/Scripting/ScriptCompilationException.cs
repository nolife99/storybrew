﻿using System;

namespace StorybrewEditor.Scripting
{
    [Serializable] public class ScriptCompilationException : Exception
    {
        public ScriptCompilationException() { }
        public ScriptCompilationException(string message) : base(message) { }
        public ScriptCompilationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
