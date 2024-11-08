namespace StorybrewCommon.Subtitles;

using System.Collections.Generic;

#pragma warning disable CS1591
public readonly struct SubtitleSet(IEnumerable<SubtitleLine> lines)
{
    public IEnumerable<SubtitleLine> Lines => lines;
}