namespace StorybrewCommon.Mapset;

using System.Globalization;

#pragma warning disable CS1591
public class OsuSpinner : OsuHitObject
{
    int endTime;
    public override float EndTime => endTime;

    public static OsuSpinner Parse(string[] values, int x, int y, int startTime, HitObjectFlag flags,
        HitSoundAddition additions, SampleSet sampleSet, SampleSet additionsSampleSet, int customSampleSet,
        float volume)
    {
        var endTime = int.Parse(values[5], CultureInfo.InvariantCulture);

        var samplePath = "";
        if (values.Length > 6)
        {
            var special = values[6];
            var specialValues = special.Split(':');

            var objectSampleSet = (SampleSet)int.Parse(specialValues[0], CultureInfo.InvariantCulture);
            var objectAdditionsSampleSet = (SampleSet)int.Parse(specialValues[1], CultureInfo.InvariantCulture);
            var objectCustomSampleSet = 0;
            if (specialValues.Length > 2)
                objectCustomSampleSet = int.Parse(specialValues[2], CultureInfo.InvariantCulture);
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