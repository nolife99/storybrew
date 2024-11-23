namespace StorybrewCommon.Util;

#pragma warning disable CS1591
public readonly record struct NamedValue(string Name, object Value)
{
    public override string ToString() => Name;
}