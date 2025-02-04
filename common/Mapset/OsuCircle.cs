﻿namespace StorybrewCommon.Mapset;

using System.Globalization;

/// <summary>Represents an osu! hit circle.</summary>
public record OsuCircle : OsuHitObject
{
    ///<summary> Parses an osu! hit circle from the given strings. </summary>
    public static OsuCircle Parse(string[] values,
        int x,
        int y,
        int startTime,
        HitObjectFlag flags,
        HitSoundAddition additions,
        SampleSet sampleSet,
        SampleSet additionsSampleSet,
        int customSampleSet,
        float volume)
    {
        var samplePath = "";
        if (values.Length <= 5)
            return new()
            {
                PlayfieldPosition = new(x, y),
                StartTime = startTime,
                Flags = flags,
                Additions = additions,
                SampleSet = sampleSet,
                AdditionsSampleSet = additionsSampleSet,
                CustomSampleSet = customSampleSet,
                Volume = volume,
                SamplePath = samplePath
            };

        var special = values[5];
        var specialValues = special.Split(':');

        var objectSampleSet = (SampleSet)int.Parse(specialValues[0], CultureInfo.InvariantCulture);

        var objectAdditionsSampleSet = (SampleSet)int.Parse(specialValues[1], CultureInfo.InvariantCulture);

        var objectCustomSampleSet = 0;
        if (specialValues.Length > 2) objectCustomSampleSet = int.Parse(specialValues[2], CultureInfo.InvariantCulture);

        var objectVolume = 0f;
        if (specialValues.Length > 3) objectVolume = int.Parse(specialValues[3], CultureInfo.InvariantCulture);

        if (specialValues.Length > 4) samplePath = specialValues[4];

        if (objectSampleSet != 0)
        {
            sampleSet = objectSampleSet;
            additionsSampleSet = objectSampleSet;
        }

        if (objectAdditionsSampleSet != 0) additionsSampleSet = objectAdditionsSampleSet;

        if (objectCustomSampleSet != 0) customSampleSet = objectCustomSampleSet;
        if (objectVolume > .001f) volume = objectVolume;
        return new()
        {
            PlayfieldPosition = new(x, y),
            StartTime = startTime,
            Flags = flags,
            Additions = additions,
            SampleSet = sampleSet,
            AdditionsSampleSet = additionsSampleSet,
            CustomSampleSet = customSampleSet,
            Volume = volume,
            SamplePath = samplePath
        };
    }
}