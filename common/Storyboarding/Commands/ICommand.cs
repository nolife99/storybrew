namespace StorybrewCommon.Storyboarding.Commands;

using System;
using System.IO;

#pragma warning disable CS1591
public interface ICommand : IComparable<ICommand>
{
    float StartTime { get; }
    float EndTime { get; }
    bool Active { get; }
    int Cost { get; }
    void WriteOsb(TextWriter writer, ExportSettings exportSettings, StoryboardTransform transform, int indentation);
    int GetHashCode();
}