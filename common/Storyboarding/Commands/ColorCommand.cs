﻿using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Storyboarding.Commands
{
#pragma warning disable CS1591
    public class ColorCommand(OsbEasing easing, double startTime, double endTime, CommandColor startValue, CommandColor endValue) : Command<CommandColor>("C", easing, startTime, endTime, startValue, endValue)
    {
        public override CommandColor ValueAtProgress(double progress) => StartValue + (EndValue - StartValue) * progress;
        public override CommandColor Midpoint(Command<CommandColor> endCommand, double progress) => StartValue + (endCommand.EndValue - StartValue) * progress;
        public override IFragmentableCommand GetFragment(double startTime, double endTime)
        {
            if (IsFragmentable)
            {
                var startValue = ValueAtTime(startTime);
                var endValue = ValueAtTime(endTime);
                return new ColorCommand(Easing, startTime, endTime, startValue, endValue);
            }
            return this;
        }
    }
}