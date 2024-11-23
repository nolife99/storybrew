namespace StorybrewCommon.Storyboarding.CommandValues;

using System;

#pragma warning disable CS1591
public readonly record struct CommandParameter : CommandValue
{
    public static readonly CommandParameter None = new(ParameterType.None), FlipHorizontal = new(ParameterType.FlipHorizontal),
        FlipVertical = new(ParameterType.FlipVertical), AdditiveBlending = new(ParameterType.AdditiveBlending);

    public readonly ParameterType Type;
    CommandParameter(ParameterType type) => Type = type;

    public string ToOsbString(ExportSettings exportSettings) => Type switch
    {
        ParameterType.FlipHorizontal => "H",
        ParameterType.FlipVertical => "V",
        ParameterType.AdditiveBlending => "A",
        _ => throw new InvalidOperationException("Parameter command cannot be None.")
    };

    public override string ToString() => ToOsbString(ExportSettings.Default);
    public static implicit operator bool(CommandParameter obj) => obj.Type is not ParameterType.None;
}