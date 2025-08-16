namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.IO;

/// <summary> A command that can be given to an <see cref="OsbSprite"/> to change its properties over time. </summary>
public interface ICommand : IComparable<ICommand>
{
    /// <summary> The start time of the command. </summary>
    float StartTime { get; }

    /// <summary> The end time of the command. </summary>
    float EndTime { get; }

    /// <summary> Determines if the command is fragmentable at the given time. </summary>
    bool IsFragmentableAt(float time);

    internal void WriteOsb(TextWriter writer,
        ExportSettings exportSettings,
        scoped ref readonly StoryboardTransform transform,
        int indentation);
}