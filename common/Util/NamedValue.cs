using System;

namespace StorybrewCommon.Util;

#pragma warning disable CS1591
[Serializable] public struct NamedValue
{
    public string Name;
    public object Value;

    public override readonly string ToString() => Name;
}