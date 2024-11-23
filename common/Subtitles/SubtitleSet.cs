namespace StorybrewCommon.Subtitles;

using System.Collections.Generic;

#pragma warning disable CS1591
public readonly record struct SubtitleSet(IEnumerable<SubtitleLine> Lines);