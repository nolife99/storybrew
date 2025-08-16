namespace StorybrewCommon.Storyboarding.CommandValues;

using System;
using Tiny.PooledCollections.Generic.Temporary;

#pragma warning disable CS1591
public readonly record struct CommandParameter : ICommandValue<CommandParameter>
{
    public static readonly CommandParameter None = new(ParameterType.None),
        FlipHorizontal = new(ParameterType.FlipHorizontal), FlipVertical = new(ParameterType.FlipVertical),
        AdditiveBlending = new(ParameterType.AdditiveBlending);

    public readonly ParameterType Type;
    CommandParameter(ParameterType type) => Type = type;

    TempList<char> ICommandValue<CommandParameter>.ToOsbString(ExportSettings exportSettings)
        => TempList.Create([
            Type switch
            {
                ParameterType.FlipHorizontal => 'H',
                ParameterType.FlipVertical => 'V',
                ParameterType.AdditiveBlending => 'A',
                _ => throw new InvalidOperationException("Parameter command cannot be None.")
            }
        ]);

    /// <inheritdoc/>
    public static CommandParameter operator +(CommandParameter left, CommandParameter right) => left;

    /// <inheritdoc/>
    public static CommandParameter operator -(CommandParameter left, CommandParameter right) => left;

    /// <inheritdoc/>
    public static CommandParameter operator *(CommandParameter left, CommandDecimal right) => left;

    public static implicit operator bool(CommandParameter obj) => obj.Type is not ParameterType.None;
}