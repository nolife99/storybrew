namespace StorybrewCommon.Mapset;

using System.Globalization;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;
using Tiny.PooledCollections.Generic.Temporary;

/// <summary> Represents an osu! spinner. </summary>
public record OsuSpinner : OsuHitObject
{
    int endTime;

    /// <inheritdoc/>
    public override float EndTime => endTime;

    internal static OsuSpinner Parse(TempList<ValueList<char>> values,
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
        var endTime = int.Parse(values[5].AsReadOnlySpan(), CultureInfo.InvariantCulture);

        var samplePath = "";
        if (values.Count > 6)
        {
            var special = values[6];
            using var specialValues = special.AsReadOnlySpan().Split([':']);

            var objectSampleSet = (SampleSet)int.Parse(specialValues[0].AsReadOnlySpan(), CultureInfo.InvariantCulture);

            var objectAdditionsSampleSet =
                (SampleSet)int.Parse(specialValues[1].AsReadOnlySpan(), CultureInfo.InvariantCulture);

            var objectCustomSampleSet = 0;
            if (specialValues.Count > 2)
                objectCustomSampleSet = int.Parse(specialValues[2].AsReadOnlySpan(), CultureInfo.InvariantCulture);

            var objectVolume = 0f;
            if (specialValues.Count > 3)
                objectVolume = int.Parse(specialValues[3].AsReadOnlySpan(), CultureInfo.InvariantCulture);

            if (specialValues.Count > 4) samplePath = specialValues[4].AsReadOnlySpan().ToString();

            foreach (var value in specialValues) value.Dispose();

            if (objectSampleSet != 0)
            {
                sampleSet = objectSampleSet;
                additionsSampleSet = objectSampleSet;
            }

            if (objectAdditionsSampleSet != 0) additionsSampleSet = objectAdditionsSampleSet;

            if (objectCustomSampleSet != 0) customSampleSet = objectCustomSampleSet;

            if (objectVolume > .001f) volume = objectVolume;
        }

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
            SamplePath = samplePath,
            endTime = endTime
        };
    }
}