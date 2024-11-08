namespace StorybrewCommon.Util;

#pragma warning disable CS1591
public struct NamedValue
{
    public string Name;
    public object Value;

    public readonly override string ToString() => Name;
}