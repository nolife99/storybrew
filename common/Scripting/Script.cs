using System;

namespace StorybrewCommon.Scripting
{
    ///<summary> Base class for all scripts. </summary>
    public abstract class Script : MarshalByRefObject
    {
        string identifier;

        ///<summary> Script name </summary>
        public string Identifier
        {
            get => identifier;
            set
            {
                if (identifier != null) throw new InvalidOperationException("This script already has an identifier");
                identifier = value;
            }
        }
    }

#pragma warning disable CS1591
    public interface IProvider<TScript> where TScript : Script
    {
        void Initialize(string assemblyPath, string typeName);
        TScript CreateScript();
    }
}