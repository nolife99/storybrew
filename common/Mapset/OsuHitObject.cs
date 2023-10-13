using OpenTK;
using OpenTK.Graphics;
using System;
using System.Drawing;
using System.Globalization;

namespace StorybrewCommon.Mapset
{
    ///<summary> Represents a hit object in osu!. </summary>
    [Serializable] public class OsuHitObject
    {
        ///<summary> Represents the playfield size in osu!. </summary>
        public static readonly Vector2 PlayfieldSize = new Vector2(512, 384), StoryboardSize = new Vector2(640, 480);

        ///<summary> Represents the offset between the playfield and the storyboard field in osu!. </summary>
        public static readonly Vector2 PlayfieldToStoryboardOffset = new Vector2((StoryboardSize.X - PlayfieldSize.X) / 2, (StoryboardSize.Y - PlayfieldSize.Y) * .75f - 16);
        
        ///<summary> Represents the widescreen storyboard size in osu!. </summary>
        public static readonly Vector2 WidescreenStoryboardSize = new Vector2(StoryboardSize.X * 4 / 3, StoryboardSize.Y);

        ///<summary> Represents the area of the widescreen storyboard size in osu!. </summary>
        public static readonly float WidescreenStoryboardArea = WidescreenStoryboardSize.X * WidescreenStoryboardSize.Y;

        ///<summary> Represents the bounds of the storyboard size in osu!. </summary>
        public static readonly RectangleF StoryboardBounds = new RectangleF(0, 0, StoryboardSize.X, StoryboardSize.Y);

        ///<summary> Represents the bounds of the widescreen storyboard size in osu!. </summary>
        public static readonly RectangleF WidescreenStoryboardBounds = new RectangleF((StoryboardSize.X - WidescreenStoryboardSize.X) / 2, 0, WidescreenStoryboardSize.X, StoryboardSize.Y);

        ///<summary> Represents the hit object's position in osu!. </summary>
        public Vector2 PlayfieldPosition;

        ///<summary> Represents this hit object's stacking offset in osu!. </summary>
        public Vector2 StackOffset;

        ///<summary> Represents this hit object's storyboard position in osu!. </summary>
        public Vector2 Position => PlayfieldPosition + PlayfieldToStoryboardOffset;

        ///<summary> Represents this hit object's end position in osu!. </summary>
        public virtual Vector2 PlayfieldEndPosition => PlayfieldPositionAtTime(EndTime);

        ///<summary> Represents this hit object's storyboard end position in osu!. </summary>
        public Vector2 EndPosition => PlayfieldEndPosition + PlayfieldToStoryboardOffset;

        ///<summary> Represents the start time of this hit object. </summary>
        public double StartTime;

        ///<summary> Represents the end time of this hit object. </summary>
        public virtual double EndTime => StartTime;

        ///<summary> Represents the information flags of this hit object. </summary>
        public HitObjectFlag Flags;

        ///<summary> Represents the hitsound additions of this hit object. </summary>
        public HitSoundAddition Additions;

        ///<summary> Represents the sample sets of this hit object. </summary>
        public SampleSet SampleSet, AdditionsSampleSet;

        ///<summary> Represents the custom sample set index of this hit object. </summary>
        public int CustomSampleSet;

        ///<summary> Represents the stack number of this hit object. </summary>
        public int StackIndex;

        ///<summary> Represents the combo number of this hit object. </summary>
        public int ComboIndex = 1; 

        ///<summary> Represents the combo color index of this hit object. </summary>
        public int ColorIndex;

        ///<summary> Represents the volume of this hit object. </summary>
        public float Volume;

        ///<summary> Represents the sample path of this hit object. </summary>
        public string SamplePath;

        ///<summary> Represents the combo color of this hit object. </summary>
        public Color4 Color = Color4.White;

        ///<returns> Whether or not this hit object is a new combo. </returns>
        public bool NewCombo => (Flags & HitObjectFlag.NewCombo) > 0;

        ///<summary> Represents this hit object's combo color number. </summary>
        public int ComboOffset => ((int)Flags >> 4) & 7;

        ///<returns> This hit object's position at <paramref name="time"/>. </returns>
        public virtual Vector2 PlayfieldPositionAtTime(double time) => PlayfieldPosition;

        ///<returns> This hit object's storyboard position at <paramref name="time"/>. </returns>
        public Vector2 PositionAtTime(double time) => PlayfieldPositionAtTime(time) + PlayfieldToStoryboardOffset;

        ///<inheritdoc/>
        public override string ToString() => $"{(int)StartTime}, {Flags}";

        ///<summary> Parses a hit object from a given beatmap and line. </summary>
        public static OsuHitObject Parse(Beatmap beatmap, string line)
        {
            var values = line.Split(',');

            var x = int.Parse(values[0], CultureInfo.InvariantCulture);
            var y = int.Parse(values[1], CultureInfo.InvariantCulture);
            var startTime = double.Parse(values[2], CultureInfo.InvariantCulture);
            var flags = (HitObjectFlag)int.Parse(values[3], CultureInfo.InvariantCulture);
            var additions = (HitSoundAddition)int.Parse(values[4], CultureInfo.InvariantCulture);

            var timingPoint = beatmap.GetTimingPointAt((int)startTime);
            var controlPoint = beatmap.GetControlPointAt((int)startTime);

            var sampleSet = controlPoint.SampleSet;
            var additionsSampleSet = controlPoint.SampleSet;
            var customSampleSet = controlPoint.CustomSampleSet;
            var volume = controlPoint.Volume;

            if (flags.HasFlag(HitObjectFlag.Circle)) return OsuCircle.Parse(values, x, y, startTime, flags, additions, timingPoint, controlPoint, sampleSet, additionsSampleSet, customSampleSet, volume);
            else if (flags.HasFlag(HitObjectFlag.Slider)) return OsuSlider.Parse(beatmap, values, x, y, startTime, flags, additions, timingPoint, controlPoint, sampleSet, additionsSampleSet, customSampleSet, volume);
            else if (flags.HasFlag(HitObjectFlag.Hold)) return OsuHold.Parse(values, x, y, startTime, flags, additions, timingPoint, controlPoint, sampleSet, additionsSampleSet, customSampleSet, volume);
            else if (flags.HasFlag(HitObjectFlag.Spinner)) return OsuSpinner.Parse(values, x, y, startTime, flags, additions, timingPoint, controlPoint, sampleSet, additionsSampleSet, customSampleSet, volume);
            throw new NotSupportedException($"Parsing failed - the line does not contain valid hit object information: {line}");
        }
    }

    ///<summary> Represents hit object flags. </summary>
    [Flags] public enum HitObjectFlag
    {
#pragma warning disable CS1591
        Circle = 1, Slider = 2, NewCombo = 4, Spinner = 8,
        SkipColor1 = 16, SkipColor2 = 32, SkipColor3 = 64,
        Hold = 128,
        Colors = SkipColor1 | SkipColor2 | SkipColor3
    }

    ///<summary> Represents hit sound sample additions. </summary>
    [Flags] public enum HitSoundAddition
    {
        None = 0, Normal = 1, Whistle = 2, Finish = 4, Clap = 8
    }

    ///<summary> Represents hit sound sample sets. </summary>
    public enum SampleSet
    {
        None = 0, Normal = 1, Soft = 2, Drum = 3
    }
}