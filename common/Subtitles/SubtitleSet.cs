using System.Collections.Generic;
using System.Linq;

namespace StorybrewCommon.Subtitles
{
#pragma warning disable CS1591
    public readonly struct SubtitleSet(IEnumerable<SubtitleLine> lines)
    {
        readonly SubtitleLine[] lines = lines.ToArray();
        public IEnumerable<SubtitleLine> Lines => lines.ToArray();
    }
}