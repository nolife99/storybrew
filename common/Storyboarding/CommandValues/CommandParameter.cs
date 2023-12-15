using System;

namespace StorybrewCommon.Storyboarding.CommandValues;

#pragma warning disable CS1591
public readonly struct CommandParameter : CommandValue
{
    public static readonly CommandParameter None = new(ParameterType.None),
        FlipHorizontal = new(ParameterType.FlipHorizontal),
        FlipVertical = new(ParameterType.FlipVertical),
        AdditiveBlending = new(ParameterType.AdditiveBlending);

    public readonly ParameterType Type;
    CommandParameter(ParameterType type) => Type = type;

    public string ToOsbString(ExportSettings exportSettings) => Type switch
    {
        ParameterType.FlipHorizontal => "H",
        ParameterType.FlipVertical => "V",
        ParameterType.AdditiveBlending => "A",
        _ => throw new InvalidOperationException($"Parameter command cannot be None."),
    };
    public override string ToString() => ToOsbString(ExportSettings.Default);
    
    public bool Equals(CommandParameter obj) => Type == obj.Type;
    public override bool Equals(object obj) => obj is CommandParameter parameter && Equals(parameter);
    public override int GetHashCode() => ToOsbString(ExportSettings.Default)[0];

    public static bool operator ==(CommandParameter left, CommandParameter right) => left.Equals(right);
    public static bool operator !=(CommandParameter left, CommandParameter right) => !left.Equals(right);
    public static implicit operator bool(CommandParameter obj) => obj.Type != ParameterType.None;
}