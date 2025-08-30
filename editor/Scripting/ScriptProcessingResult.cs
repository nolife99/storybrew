namespace StorybrewEditor.Scripting;

using System;

public readonly struct ScriptProcessingResult<T>
{
    public ScriptProcessingResult(T value) => Value = value;
    public ScriptProcessingResult(Exception error) => Error = error;

    public bool Success => Error is null;

    public readonly T Value;
    public readonly Exception Error;
}