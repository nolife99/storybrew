namespace StorybrewCommon.Util;

#pragma warning disable CS1591
public readonly struct NamedValue
{
    public string Name { get; init; }
    public object Value { get; init; }

    public override string ToString() => Name;
}