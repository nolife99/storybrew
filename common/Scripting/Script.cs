namespace StorybrewCommon.Scripting;

using System;

///<summary> Defines a script to execute. </summary>
public abstract class Script
{
    string identifier;

    ///<summary> Script name </summary>
    public string Identifier
    {
        get => identifier;
        set
            => identifier = identifier is null ? value
                : throw new InvalidOperationException("This script already has an identifier");
    }
}