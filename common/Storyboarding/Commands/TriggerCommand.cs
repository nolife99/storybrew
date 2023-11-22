using StorybrewCommon.Util;
using System.Linq;

namespace StorybrewCommon.Storyboarding.Commands
{
#pragma warning disable CS1591
    public class TriggerCommand : CommandGroup
    {
        public string TriggerName { get; set; }
        public int Group { get; set; }
        public override bool Active => false;

        public TriggerCommand(string triggerName, double startTime, double endTime, int group = 0)
        {
            TriggerName = triggerName;
            StartTime = startTime;
            EndTime = endTime;
            Group = group;
        }

        protected override string GetCommandGroupHeader(ExportSettings exportSettings) =>
            $"T,{TriggerName},{((int)StartTime).ToString(exportSettings.NumberFormat)},{((int)EndTime).ToString(exportSettings.NumberFormat)},{Group.ToString(exportSettings.NumberFormat)}";

        public override int GetHashCode()
        {
            var header = new HashCode(HashCode.Combine('T', TriggerName, StartTime, EndTime, Group));
            foreach (var command in commands) header.Add(command);
            return header.ToHashCode();
        }

        public override bool Equals(object obj) => obj is TriggerCommand loop && Equals(loop);
        public bool Equals(TriggerCommand other)
            => other.TriggerName == TriggerName && other.StartTime == StartTime && other.EndTime == EndTime && other.Group == Group && commands.SequenceEqual(other.commands);
    }
}