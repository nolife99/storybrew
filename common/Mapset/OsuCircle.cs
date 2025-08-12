namespace StorybrewCommon.Mapset;

using System.Globalization;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Value;
using Tiny.PooledCollections.Generic.Value.Internals;

/// <summary> Represents an osu! hit circle. </summary>
public record OsuCircle : OsuHitObject
{
    internal static OsuCircle Parse(TempList<ValueArray<char>> values,
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
        if (values.Count <= 5)
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

        var special = values[5].AsReadOnlySpan();
        using var specialValues = special.Split([':']);

        var objectSampleSet = (SampleSet)int.Parse(special[specialValues[0]], CultureInfo.InvariantCulture);

        var objectAdditionsSampleSet = (SampleSet)int.Parse(special[specialValues[1]], CultureInfo.InvariantCulture);

        var objectCustomSampleSet = 0;
        if (specialValues.Count > 2)
            objectCustomSampleSet = int.Parse(special[specialValues[2]], CultureInfo.InvariantCulture);

        var objectVolume = 0f;
        if (specialValues.Count > 3) objectVolume = int.Parse(special[specialValues[3]], CultureInfo.InvariantCulture);

        if (specialValues.Count > 4) samplePath = special[specialValues[4]].ToString();

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