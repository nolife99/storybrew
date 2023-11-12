using System;
using System.Collections.Generic;
using System.Linq;

namespace StorybrewCommon.Storyboarding.Commands
{
#pragma warning disable CS1591
    public class LoopCommand : CommandGroup, IFragmentableCommand
    {
        public int LoopCount { get; set; }
        public override double EndTime
        {
            get => StartTime + (CommandsEndTime * LoopCount);
            set => LoopCount = (int)((value - StartTime) / CommandsEndTime);
        }
        public LoopCommand(double startTime, int loopCount)
        {
            StartTime = startTime;
            LoopCount = loopCount;
        }

        public override void EndGroup()
        {
            var commandsStartTime = CommandsStartTime;
            if (commandsStartTime > 0)
            {
                StartTime += commandsStartTime;
                foreach (var command in commands) ((IOffsetable)command).Offset(-commandsStartTime);
            }
            base.EndGroup();
        }
        protected override string GetCommandGroupHeader(ExportSettings exportSettings)
            => $"L,{(exportSettings.UseFloatForTime ? (float)StartTime : (int)StartTime).ToString(exportSettings.NumberFormat)},{LoopCount.ToString(exportSettings.NumberFormat)}";

        public override int GetHashCode()
        {
            var header = new StorybrewCommon.Util.HashCode(StorybrewCommon.Util.HashCode.Combine('L', StartTime, LoopCount));
            foreach (var command in commands) header.Add(command);
            return header.ToHashCode();
        }

        public override bool Equals(object obj) => obj is LoopCommand loop && Equals(loop);
        public bool Equals(LoopCommand other)
            => other.StartTime == StartTime && other.LoopCount == LoopCount && commands.SequenceEqual(other.commands);

        public bool IsFragmentable => LoopCount > 1;

        public IFragmentableCommand GetFragment(double startTime, double endTime)
        {
            if (IsFragmentable && (endTime - startTime) % CommandsDuration == 0 && (startTime - StartTime) % CommandsDuration == 0)
            {
                var loopCount = (int)Math.Round((endTime - startTime) / CommandsDuration);
                var loopFragment = new LoopCommand(startTime, loopCount);
                foreach (var c in commands) loopFragment.Add(c);
                return loopFragment;
            }
            return this;
        }
        public IEnumerable<int> GetNonFragmentableTimes()
        {
            var nonFragmentableTimes = new HashSet<int>(LoopCount * (int)(CommandsDuration - 1));
            for (var i = 0; i < LoopCount; i++) for (var j = 0; j < CommandsDuration - 1; ++j) nonFragmentableTimes.Add((int)StartTime + i * (int)CommandsDuration + 1 + j);
            return nonFragmentableTimes;
        }
    }
}