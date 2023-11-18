using System.Collections.Generic;

namespace StorybrewCommon.Subtitles
{
#pragma warning disable CS1591
    public readonly struct SubtitleSet(IEnumerable<SubtitleLine> lines)
    {
        readonly HashSet<SubtitleLine> lines = new(lines);
        public IEnumerable<SubtitleLine> Lines => lines;
    }
}