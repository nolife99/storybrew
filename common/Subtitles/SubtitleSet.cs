using System.Collections.Generic;

namespace StorybrewCommon.Subtitles
{
#pragma warning disable CS1591
    public struct SubtitleSet
    {
        readonly HashSet<SubtitleLine> lines;
        public IEnumerable<SubtitleLine> Lines => lines;

        public SubtitleSet(IEnumerable<SubtitleLine> lines) => this.lines = new HashSet<SubtitleLine>(lines);
    }
}