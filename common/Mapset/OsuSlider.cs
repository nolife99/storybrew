namespace StorybrewCommon.Mapset;

using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Curves;
using Storyboarding.CommandValues;

/// <summary>
/// Represents an osu! slider.
/// </summary>
public class OsuSlider(OsuSliderNode[] nodes, OsuSliderControlPoint[] controlPoints) : OsuHitObject
{
    Curve curve;

    /// <summary>
    /// The curve type of this slider.
    /// </summary>
    public SliderCurveType CurveType { get; init; }

    ///<summary> The total distance the slider ball travels, in osu!pixels. </summary>
    public float Length { get; init; }

    CommandPosition playfieldTipPosition;

    ///<summary> The time it takes for the slider to complete its body in milliseconds. </summary>
    public float TravelDuration { get; init; }

    ///<summary> The time it takes for the slider to complete its body in beats. </summary>
    public float TravelDurationBeats { get; init; }

    /// <summary>
    /// Gets an enumeration of nodes that make up the slider.
    /// Each node contains the sample set and sample volume at a specific time in the slider.
    /// </summary>
    public IEnumerable<OsuSliderNode> Nodes => nodes;

    /// <summary>
    /// Gets the number of nodes in this slider.
    /// </summary>
    public int NodeCount => nodes.Length;

    /// <summary>
    /// Gets an enumeration of control points that make up the slider's curve.
    /// </summary>
    public IEnumerable<OsuSliderControlPoint> ControlPoints => controlPoints;

    /// <summary>
    /// Gets the number of control points in this slider.
    /// </summary>
    public int ControlPointCount => controlPoints.Length;

    /// <inheritdoc/>
    public override float EndTime => StartTime + TravelCount * TravelDuration;

    /// <summary>
    /// Gets the curve that represents this slider's shape.
    /// </summary>
    public Curve Curve
    {
        get
        {
            if (curve is null) generateCurve();
            return curve;
        }
    }

    /// <summary>
    /// Gets the position of the end of the slider's body in playfield coordinates.
    /// </summary>
    public CommandPosition PlayfieldTipPosition
    {
        get
        {
            if (curve is null) generateCurve();
            return playfieldTipPosition;
        }
    }
    
    /// <summary>
    /// Gets the position of the end of the slider's body in storyboard coordinates.
    /// </summary>
    public CommandPosition TipPosition => PlayfieldTipPosition + PlayfieldToStoryboardOffset;

    /// <summary> How many times the slider ball travels across the slider's body. </summary>
    public int TravelCount => nodes.Length - 1;

    ///<summary> How many times the slider ball hits a repeat. </summary>
    public int RepeatCount => nodes.Length - 2;

    /// <inheritdoc/>
    public override CommandPosition PlayfieldPositionAtTime(float time)
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

        if (curve is null) generateCurve();
        return curve.PositionAtDistance(Length * progress);
    }

    /// <inheritdoc/>
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
                if (controlPoints.Length < 2 || !CircleCurve.IsValid(PlayfieldPosition, controlPoints[0].PlayfieldPosition,
                    controlPoints[1].PlayfieldPosition)) goto case SliderCurveType.Linear;

                curve = generateCircleCurve();
                break;

            case SliderCurveType.Linear:
            default:
                curve = generateLinearCurve();
                break;
        }

        playfieldTipPosition = curve.PositionAtDistance(Length);
    }

    CircleCurve generateCircleCurve()
        => new(PlayfieldPosition, controlPoints[0].PlayfieldPosition, controlPoints[1].PlayfieldPosition);

    CompositeCurve generateBezierCurve()
    {
        List<BezierCurve> curves = [];

        List<Vector2> curvePoints = [];
        var precision = (int)(Length / 2);

        var previousPosition = (Vector2)PlayfieldPosition;
        curvePoints.Add(previousPosition);

        foreach (var controlPoint in controlPoints)
        {
            if (controlPoint.PlayfieldPosition == previousPosition)
            {
                if (curvePoints.Count > 1) curves.Add(new(curvePoints, precision));
                curvePoints = [];
            }

            curvePoints.Add(controlPoint.PlayfieldPosition);
            previousPosition = controlPoint.PlayfieldPosition;
        }

        if (curvePoints.Count > 1) curves.Add(new(curvePoints, precision));
        return new(curves);
    }

    CatmullCurve generateCatmullCurve()
    {
        var curvePoints = new Vector2[controlPoints.Length + 1];
        curvePoints[0] = PlayfieldPosition;
        for (var i = 0; i < ControlPointCount; ++i) curvePoints[i + 1] = controlPoints[i].PlayfieldPosition;
        return new(curvePoints, (int)(Length / 2));
    }

    CompositeCurve generateLinearCurve()
    {
        var curves = new BezierCurve[controlPoints.Length];

        var previousPoint = PlayfieldPosition;
        for (var i = 0; i < controlPoints.Length; ++i)
        {
            curves[i] = new([previousPoint, controlPoints[i].PlayfieldPosition], 0);
            previousPoint = controlPoints[i].PlayfieldPosition;
        }

        return new(curves);
    }

    ///<summary> Parses an osu! slider from the given strings. </summary>
    public static OsuSlider Parse(Beatmap beatmap,
        string[] values,
        int x,
        int y,
        int startTime,
        HitObjectFlag flags,
        HitSoundAddition additions,
        ControlPoint timingPoint,
        ControlPoint controlPoint,
        SampleSet sampleSet,
        SampleSet additionsSampleSet,
        int customSampleSet,
        float volume)
    {
        var slider = values[5];
        var sliderValues = slider.Split('|');

        var curveType = sliderValues[0] switch
        {
            "L" => SliderCurveType.Linear,
            "C" => SliderCurveType.Catmull,
            "B" => SliderCurveType.Bezier,
            "P" => SliderCurveType.Perfect,
            _ => SliderCurveType.Unknown
        };

        var sliderControlPointCount = sliderValues.Length - 1;
        var sliderControlPoints = new OsuSliderControlPoint[sliderControlPointCount];

        for (var i = 0; i < sliderControlPointCount; i++)
        {
            var controlPointValues = sliderValues[i + 1].Split(':');
            var controlPointX = float.Parse(controlPointValues[0], CultureInfo.InvariantCulture);
            var controlPointY = float.Parse(controlPointValues[1], CultureInfo.InvariantCulture);
            sliderControlPoints[i] = new Vector2(controlPointX, controlPointY);
        }

        var nodeCount = int.Parse(values[6], CultureInfo.InvariantCulture) + 1;
        var length = float.Parse(values[7], CultureInfo.InvariantCulture);

        var sliderMultiplierLessLength = length / beatmap.SliderMultiplier;
        var travelDurationBeats = sliderMultiplierLessLength / 100 * controlPoint.SliderMultiplier;
        var travelDuration = timingPoint.BeatDuration * travelDurationBeats;

        var sliderNodes = new OsuSliderNode[nodeCount];
        for (var i = 0; i < nodeCount; i++)
        {
            var nodeStartTime = startTime + i * travelDuration;
            var nodeControlPoint = beatmap.GetTimingPointAt((int)nodeStartTime);
            sliderNodes[i] = new()
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
                var nodeAdditionsSampleSet = int.Parse(sampleAndAdditionSampleSetValues2[1], CultureInfo.InvariantCulture);

                if (nodeSampleSet != 0)
                {
                    node.SampleSet = nodeSampleSet;
                    node.AdditionsSampleSet = nodeSampleSet;
                }

                if (nodeAdditionsSampleSet != 0) node.AdditionsSampleSet = (SampleSet)nodeAdditionsSampleSet;
            }
        }

        var samplePath = "";
        if (values.Length < 11)
            return new(sliderNodes, sliderControlPoints)
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
                CurveType = curveType,
                Length = length,
                TravelDurationBeats = travelDurationBeats,
                TravelDuration = travelDuration
            };

        var special = values[10];
        var specialValues = special.Split(':');

        var objectSampleSet = (SampleSet)int.Parse(specialValues[0], CultureInfo.InvariantCulture);
        var objectAdditionsSampleSet = int.Parse(specialValues[1], CultureInfo.InvariantCulture);
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

        if (objectAdditionsSampleSet != 0) additionsSampleSet = (SampleSet)objectAdditionsSampleSet;
        if (objectCustomSampleSet != 0) customSampleSet = objectCustomSampleSet;
        if (objectVolume > .001f) volume = objectVolume;

        return new(sliderNodes, sliderControlPoints)
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
            CurveType = curveType,
            Length = length,
            TravelDurationBeats = travelDurationBeats,
            TravelDuration = travelDuration
        };
    }
}

/// <summary>
///     Represents a slider node in an osu! slider.
/// </summary>
public class OsuSliderNode
{
    /// <summary>
    ///     The hit sound additions of this node.
    /// </summary>
    public HitSoundAddition Additions { get; set; }

    /// <summary>
    ///     The custom sample set of this node.
    /// </summary>
    public int CustomSampleSet { get; set; }

    /// <summary>
    ///     The sample set of this node.
    /// </summary>
    public SampleSet SampleSet { get; set; }

    /// <summary>
    ///     The additions sample set of this node.
    /// </summary>
    public SampleSet AdditionsSampleSet { get; set; }

    /// <summary>
    ///     The time in milliseconds of this node.
    /// </summary>
    public float Time { get; set; }

    /// <summary>
    ///     The volume of this node.
    /// </summary>
    public float Volume { get; set; }
}

/// <summary>
///     Represents a control point in an osu! slider.
/// </summary>
public class OsuSliderControlPoint
{
    /// <summary>
    ///     The playfield position of this control point.
    /// </summary>
    public Vector2 PlayfieldPosition { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OsuSliderControlPoint"/> class.
    /// </summary>
    /// <param name="position"> The playfield position of this control point. </param>
    OsuSliderControlPoint(Vector2 position) => PlayfieldPosition = position;

    /// <summary>
    ///     Performs an implicit conversion from <see cref="Vector2"/> to <see cref="OsuSliderControlPoint"/>.
    /// </summary>
    /// <param name="position"> The playfield position of this control point. </param>
    /// <returns> The result of the conversion. </returns>
    public static implicit operator OsuSliderControlPoint(Vector2 position) => new(position);
}

/// <summary>
///     The curve type of a slider.
/// </summary>
public enum SliderCurveType
{
    /// <summary>
    ///     The curve type is unknown.
    /// </summary>
    Unknown,

    /// <summary>
    ///     The curve is linear.
    /// </summary>
    Linear,

    /// <summary>
    ///     The curve is a Catmull-Rom spline.
    /// </summary>
    Catmull,

    /// <summary>
    ///     The curve is a bézier curve.
    /// </summary>
    Bezier,

    /// <summary>
    ///     The curve is a perfect circular arc.
    /// </summary>
    Perfect
}