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

    /// <summary> Writes the command to a .osb file. </summary>
    /// <param name="writer"> The writer to write the command to. </param>
    /// <param name="exportSettings"> The export settings to use when writing the command. </param>
    /// <param name="transform"> The transform to apply to the command when writing it. </param>
    /// <param name="indentation"> The number of spaces to indent the command with. </param>
    void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation);
}