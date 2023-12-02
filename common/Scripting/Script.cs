using System;

namespace StorybrewCommon.Scripting;

///<summary> Base class for all scripts. </summary>
public abstract class Script : MarshalByRefObject
{
    string identifier;

    ///<summary> Script name </summary>
    public string Identifier
    {
        get => identifier;
        set => identifier = identifier is null ? value : throw new InvalidOperationException("This script already has an identifier");
    }
}