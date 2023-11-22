using StorybrewCommon.Curves;
using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;

namespace StorybrewCommon.Mapset
{
#pragma warning disable CS1591
    [Serializable] public class OsuSlider : OsuHitObject
    {
        readonly OsuSliderNode[] nodes;
        public IEnumerable<OsuSliderNode> Nodes => nodes;
        public int NodeCount => nodes.Length;

        readonly OsuSliderControlPoint[] controlPoints;
        public IEnumerable<OsuSliderControlPoint> ControlPoints => controlPoints;
        public int ControlPointCount => controlPoints.Length;

        public override double EndTime => StartTime + TravelCount * TravelDuration;

        Curve curve;
        public Curve Curve
        {
            get
            {
                if (curve is null) generateCurve();
                return curve;
            }
        }

        CommandPosition playfieldTipPosition;
        public CommandPosition PlayfieldTipPosition
        {
            get
            {
                if (curve is null) generateCurve();
                return playfieldTipPosition;
            }
        }

        public CommandPosition TipPosition => PlayfieldTipPosition + PlayfieldToStoryboardOffset;

        ///<summary> The total distance the slider ball travels, in osu!pixels. </summary>
        public double Length;

        ///<summary> The time it takes for the slider ball to travels across the slider's body in beats. </summary>
        public double TravelDurationBeats;

        ///<summary> The time it takes for the slider ball to travels across the slider's body in milliseconds. </summary>
        public double TravelDuration;

        /// <summary> How many times the slider ball travels across the slider's body. </summary>
        public int TravelCount => nodes.Length - 1;

        ///<summary> How many times the slider ball hits a repeat. </summary>
        public int RepeatCount => nodes.Length - 2;

        public SliderCurveType CurveType;

        public OsuSlider(OsuSliderNode[] nodes, OsuSliderControlPoint[] controlPoints)
        {
            this.nodes = nodes;
            this.controlPoints = controlPoints;
        }

        public override CommandPosition PlayfieldPositionAtTime(double time)
        {
            if (time <= StartTime) return PlayfieldPosition;
            if (EndTime <= time) return TravelCount % 2 == 0 ? PlayfieldPosition : PlayfieldTipPosition;

            var elapsedSinceStartTime = time - StartTime;

            var repeatAtTime = 1;
            var progressDuration = elapsedSinceStartTime;
            while (progressDuration > TravelDuration)
            {
                progressDuration -= TravelDuration;
                ++repeatAtTime;
            }

            var progress = progressDuration / TravelDuration;
            var reversed = (repeatAtTime & 1) == 0;
            if (reversed) progress = 1 - progress;

            if (curve == null) generateCurve();
            return curve.PositionAtDistance(Length * progress);
        }

        public override string ToString() => $"{base.ToString()}, {CurveType}, {TravelCount}x";

        void generateCurve()
        {
            switch (CurveType)
            {
                case SliderCurveType.Catmull:
                    if (controlPoints.Length == 1) goto case SliderCurveType.Linear;
                    curve = generateCatmullCurve();
                    break;

                case SliderCurveType.Bezier:
                    if (controlPoints.Length == 1) goto case SliderCurveType.Linear;
                    curve = generateBezierCurve();
                    break;

                case SliderCurveType.Perfect:
                    if (controlPoints.Length > 2) goto case SliderCurveType.Bezier;
                    if (controlPoints.Length < 2 || !CircleCurve.IsValid(
                        PlayfieldPosition, controlPoints[0].PlayfieldPosition, controlPoints[1].PlayfieldPosition)) goto case SliderCurveType.Linear;
                    curve = generateCircleCurve();
                    break;

                case SliderCurveType.Linear:
                default: curve = generateLinearCurve(); break;
            }
            playfieldTipPosition = curve.PositionAtDistance(Length);
        }

        Curve generateCircleCurve() => new CircleCurve(PlayfieldPosition, controlPoints[0].PlayfieldPosition, controlPoints[1].PlayfieldPosition);
        Curve generateBezierCurve()
        {
            var curves = new List<Curve>();

            var curvePoints = new List<CommandPosition>();
            var precision = (int)Math.Ceiling(Length);

            var previousPosition = PlayfieldPosition;
            curvePoints.Add(previousPosition);

            foreach (var controlPoint in controlPoints)
            {
                if (controlPoint.PlayfieldPosition == previousPosition)
                {
                    if (curvePoints.Count > 1) curves.Add(new BezierCurve(curvePoints, precision));
                    curvePoints = new List<CommandPosition>();
                }

                curvePoints.Add(controlPoint.PlayfieldPosition);
                previousPosition = controlPoint.PlayfieldPosition;
            }

            if (curvePoints.Count > 1) curves.Add(new BezierCurve(curvePoints, precision));
            return new CompositeCurve(curves);
        }
        Curve generateCatmullCurve()
        {
            var curvePoints = new CommandPosition[controlPoints.Length + 1];
            curvePoints[0] = PlayfieldPosition;
            for (var i = 0; i < ControlPointCount; ++i) curvePoints[i + 1] = controlPoints[i].PlayfieldPosition;

            var precision = (int)Math.Ceiling(Length);
            return new CatmullCurve(curvePoints, precision);
        }
        Curve generateLinearCurve()
        {
            var curves = new Curve[controlPoints.Length];

            var previousPoint = PlayfieldPosition;
            for (var i = 0; i < controlPoints.Length; ++i)
            {
                curves[i] = new BezierCurve(new CommandPosition[]
                {
                    previousPoint,
                    controlPoints[i].PlayfieldPosition
                }, 0);
                previousPoint = controlPoints[i].PlayfieldPosition;
            }
            return new CompositeCurve(curves);
        }

        public static OsuSlider Parse(Beatmap beatmap, string[] values, int x, int y, double startTime, HitObjectFlag flags, HitSoundAddition additions, ControlPoint timingPoint, ControlPoint controlPoint, SampleSet sampleSet, SampleSet additionsSampleSet, int customSampleSet, float volume)
        {
            var slider = values[5];
            var sliderValues = slider.Split('|');

            var curveType = LetterToCurveType(sliderValues[0]);
            var sliderControlPointCount = sliderValues.Length - 1;
            var sliderControlPoints = new OsuSliderControlPoint[sliderControlPointCount];
            for (var i = 0; i < sliderControlPointCount; i++)
            {
                var controlPointValues = sliderValues[i + 1].Split(':');
                var controlPointX = float.Parse(controlPointValues[0], CultureInfo.InvariantCulture);
                var controlPointY = float.Parse(controlPointValues[1], CultureInfo.InvariantCulture);
                sliderControlPoints[i] = new CommandPosition(controlPointX, controlPointY);
            }

            var nodeCount = int.Parse(values[6], CultureInfo.InvariantCulture) + 1;
            var length = double.Parse(values[7], CultureInfo.InvariantCulture);

            var sliderMultiplierLessLength = length / beatmap.SliderMultiplier;
            var travelDurationBeats = sliderMultiplierLessLength / 100 * controlPoint.SliderMultiplier;
            var travelDuration = timingPoint.BeatDuration * travelDurationBeats;

            var sliderNodes = new OsuSliderNode[nodeCount];
            for (var i = 0; i < nodeCount; i++)
            {
                var nodeStartTime = startTime + i * travelDuration;
                var nodeControlPoint = beatmap.GetTimingPointAt((int)nodeStartTime);
                sliderNodes[i] = new OsuSliderNode
                {
                    Time = nodeStartTime,
                    SampleSet = nodeControlPoint.SampleSet,
                    AdditionsSampleSet = nodeControlPoint.SampleSet,
                    CustomSampleSet = nodeControlPoint.CustomSampleSet,
                    Volume = nodeControlPoint.Volume,
                    Additions = additions
                };
            }
            if (values.Length > 8)
            {
                var sliderAddition = values[8];
                var sliderAdditionValues = sliderAddition.Split('|');
                for (var i = 0; i < sliderAdditionValues.Length; i++)
                {
                    var node = sliderNodes[i];
                    var nodeAdditions = (HitSoundAddition)int.Parse(sliderAdditionValues[i], CultureInfo.InvariantCulture);
                    node.Additions = nodeAdditions;
                }
            }
            if (values.Length > 9)
            {
                var sampleAndAdditionSampleSet = values[9];
                var sampleAndAdditionSampleSetValues = sampleAndAdditionSampleSet.Split('|');
                for (var i = 0; i < sampleAndAdditionSampleSetValues.Length; i++)
                {
                    var node = sliderNodes[i];
                    var sampleAndAdditionSampleSetValues2 = sampleAndAdditionSampleSetValues[i].Split(':');
                    var nodeSampleSet = (SampleSet)int.Parse(sampleAndAdditionSampleSetValues2[0], CultureInfo.InvariantCulture);
                    var nodeAdditionsSampleSet = (SampleSet)int.Parse(sampleAndAdditionSampleSetValues2[1], CultureInfo.InvariantCulture);

                    if (nodeSampleSet != 0)
                    {
                        node.SampleSet = nodeSampleSet;
                        node.AdditionsSampleSet = nodeSampleSet;
                    }
                    if (nodeAdditionsSampleSet != 0) node.AdditionsSampleSet = nodeAdditionsSampleSet;
                }
            }

            string samplePath = "";
            if (values.Length > 10)
            {
                var special = values[10];
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
                if (objectVolume > .001) volume = objectVolume;
            }

            return new OsuSlider(sliderNodes, sliderControlPoints)
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
                // Slider specific
                CurveType = curveType,
                Length = length,
                TravelDurationBeats = travelDurationBeats,
                TravelDuration = travelDuration
            };
        }
        public static SliderCurveType LetterToCurveType(string letter)
        {
            switch (letter)
            {
                case "L": return SliderCurveType.Linear;
                case "C": return SliderCurveType.Catmull;
                case "B": return SliderCurveType.Bezier;
                case "P": return SliderCurveType.Perfect;
                default: return SliderCurveType.Unknown;
            }
        }
    }
    [Serializable] public class OsuSliderNode
    {
        public double Time;
        public HitSoundAddition Additions;
        public SampleSet SampleSet, AdditionsSampleSet;
        public int CustomSampleSet;
        public float Volume;
    }
    [Serializable] public class OsuSliderControlPoint
    {
        public CommandPosition PlayfieldPosition;
        public CommandPosition Position => PlayfieldPosition + OsuHitObject.PlayfieldToStoryboardOffset;

        public OsuSliderControlPoint(CommandPosition position) => PlayfieldPosition = position;

        public static implicit operator OsuSliderControlPoint(CommandPosition position) => new OsuSliderControlPoint(position);
    }
    public enum SliderCurveType
    {
        Unknown, Linear, Catmull, Bezier, Perfect
    }
}