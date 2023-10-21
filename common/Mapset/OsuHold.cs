﻿using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace StorybrewCommon.Mapset
{
#pragma warning disable CS1591
    [Serializable] public class OsuHold : OsuHitObject
    {
        double endTime;
        public override double EndTime => endTime;

        internal OsuHold() { }
        protected OsuHold(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            endTime = info.GetDouble("endTime");
        }
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("endTime", endTime);
        }

        public static OsuHold Parse(string[] values, int x, int y, double startTime, HitObjectFlag flags, HitSoundAddition additions, ControlPoint timingPoint, ControlPoint controlPoint, SampleSet sampleSet, SampleSet additionsSampleSet, int customSampleSet, float volume)
        {
            string samplePath = string.Empty;

            var special = values[5];
            var specialValues = special.Split(':');

            var endTime = double.Parse(specialValues[0], CultureInfo.InvariantCulture);
            var objectSampleSet = (SampleSet)int.Parse(specialValues[1], CultureInfo.InvariantCulture);
            var objectAdditionsSampleSet = (SampleSet)int.Parse(specialValues[2], CultureInfo.InvariantCulture);
            var objectCustomSampleSet = int.Parse(specialValues[3], CultureInfo.InvariantCulture);
            var objectVolume = 0f;
            if (specialValues.Length > 4) objectVolume = int.Parse(specialValues[4], CultureInfo.InvariantCulture);
            if (specialValues.Length > 5) samplePath = specialValues[5];

            if (objectSampleSet != 0)
            {
                sampleSet = objectSampleSet;
                additionsSampleSet = objectSampleSet;
            }
            if (objectAdditionsSampleSet != 0) additionsSampleSet = objectAdditionsSampleSet;
            if (objectCustomSampleSet != 0) customSampleSet = objectCustomSampleSet;
            if (objectVolume > .001) volume = objectVolume;

            return new OsuHold
            {
                PlayfieldPosition = new CommandPosition(x, y),
                StartTime = startTime,
                Flags = flags,
                Additions = additions,
                SampleSet = sampleSet,
                AdditionsSampleSet = additionsSampleSet,
                CustomSampleSet = customSampleSet,
                Volume = volume,
                SamplePath = samplePath,
                endTime = endTime
            };
        }
    }
}