namespace StorybrewCommon.Util;

#pragma warning disable CS1591
public struct NamedValue
{
    public string Name;
    public object Value;

    public override readonly string ToString() => Name;
}