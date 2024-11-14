namespace StorybrewCommon.Mapset;

using System.Globalization;

/// <summary>
///     Represents an osu!mania hold note.
/// </summary>
public class OsuHold : OsuHitObject
{
    int endTime;

    /// <inheritdoc/>
    public override float EndTime => endTime;

    ///<summary> Parses an osu!mania hold note from the given strings. </summary>
    public static OsuHold Parse(string[] values,
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

        var special = values[5];
        var specialValues = special.Split(':');

        var endTime = int.Parse(specialValues[0], CultureInfo.InvariantCulture);
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
            SamplePath = samplePath,
            endTime = endTime
        };
    }
}