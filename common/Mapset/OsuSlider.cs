namespace StorybrewCommon.Mapset;

using System;
using System.Globalization;
using System.Numerics;
using BrewLib.Util;
using Curves;
using Tiny.PooledCollections.Generic.StructBased;
using Tiny.PooledCollections.Generic.StructBased.Internals;
using Tiny.PooledCollections.Generic.Temporary;
using Tiny.PooledCollections.Generic.Temporary.Internals;

/// <summary>Represents an osu! slider.</summary>
public record OsuSlider(OsuSliderNode[] nodes, Vector2[] controlPoints) : OsuHitObject
{
    Curve curve;

    Vector2 playfieldTipPosition;

    /// <summary>The curve type of this slider.</summary>
    public SliderCurveType CurveType { get; init; }

    ///<summary> The total distance the slider ball travels, in osu!pixels. </summary>
    public float Length { get; init; }

    ///<summary> The time it takes for the slider to complete its body in milliseconds. </summary>
    public float TravelDuration { get; init; }

    ///<summary> The time it takes for the slider to complete its body in beats. </summary>
    public float TravelDurationBeats { get; init; }

    /// <summary>
    ///     Gets an enumeration of nodes that make up the slider. Each node contains the sample set and sample volume at a
    ///     specific time in the slider.
    /// </summary>
    public ReadOnlySpan<OsuSliderNode> Nodes => nodes;

    /// <summary>Gets the number of nodes in this slider.</summary>
    public int NodeCount => nodes.Length;

    /// <summary>Gets an enumeration of control points that make up the slider's curve.</summary>
    public ReadOnlySpan<Vector2> ControlPoints => controlPoints;

    /// <summary>Gets the number of control points in this slider.</summary>
    public int ControlPointCount => controlPoints.Length;

    /// <inheritdoc/>
    public override float EndTime => StartTime + TravelCount * TravelDuration;

    /// <summary>Gets the curve that represents this slider's shape.</summary>
    public Curve Curve
    {
        get
        {
            if (curve is null) generateCurve();
            return curve;
        }
    }

    /// <summary>Gets the position of the end of the slider's body in playfield coordinates.</summary>
    public Vector2 PlayfieldTipPosition
    {
        get
        {
            if (curve is null) generateCurve();
            return playfieldTipPosition;
        }
    }

    /// <summary>Gets the position of the end of the slider's body in storyboard coordinates.</summary>
    public Vector2 TipPosition => PlayfieldTipPosition + PlayfieldToStoryboardOffset;

    /// <summary> How many times the slider ball travels across the slider's body. </summary>
    public int TravelCount => nodes.Length - 1;

    ///<summary> How many times the slider ball hits a repeat. </summary>
    public int RepeatCount => nodes.Length - 2;

    /// <inheritdoc/>
    public override Vector2 PlayfieldPositionAtTime(float time)
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
                if (controlPoints.Length < 2 || !CircleCurve.IsValid(PlayfieldPosition, controlPoints[0], controlPoints[1]))
                    goto case SliderCurveType.Linear;

                curve = new CircleCurve(PlayfieldPosition, controlPoints[0], controlPoints[1]);
                break;

            case SliderCurveType.Linear:
            default: curve = generateLinearCurve(); break;
        }

        playfieldTipPosition = curve.PositionAtDistance(Length);
    }

    CompositeCurve generateBezierCurve()
    {
        using var curves = TempList.Create<Curve>();
        using var curvePoints = TempList.Create<Vector2>();

        var previousPosition = PlayfieldPosition;
        curvePoints.Add(previousPosition);

        foreach (var controlPoint in controlPoints)
        {
            if (controlPoint == previousPosition)
            {
                if (curvePoints.Count > 1) curves.Add(new BezierCurve(curvePoints.AsReadOnlySpan()));
                curvePoints.Clear();
            }

            curvePoints.Add(controlPoint);
            previousPosition = controlPoint;
        }

        if (curvePoints.Count > 1) curves.Add(new BezierCurve(curvePoints.AsReadOnlySpan()));
        return new(curves.AsReadOnlySpan());
    }

    CatmullCurve generateCatmullCurve()
    {
        using var curvePoints = TempList.Create<Vector2>(controlPoints.Length + 1);
        curvePoints.Add(PlayfieldPosition);

        foreach (var t in controlPoints) curvePoints.Add(t);

        return new(curvePoints.AsReadOnlySpan());
    }

    CompositeCurve generateLinearCurve()
    {
        using var curves = TempArray.Create<Curve>(controlPoints.Length);

        var previousPoint = PlayfieldPosition;
        for (var i = 0; i < controlPoints.Length; ++i)
        {
            curves[i] = new BezierCurve([previousPoint, controlPoints[i]]);
            previousPoint = controlPoints[i];
        }

        return new(curves.AsReadOnlySpan());
    }

    internal static OsuSlider Parse(Beatmap beatmap,
        TempList<ValueList<char>> values,
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
        using var sliderValues = slider.AsReadOnlySpan().Split(['|']);

        var curveType = sliderValues[0].AsReadOnlySpan() switch
        {
            "L" => SliderCurveType.Linear,
            "C" => SliderCurveType.Catmull,
            "B" => SliderCurveType.Bezier,
            "P" => SliderCurveType.Perfect,
            _ => SliderCurveType.Unknown
        };

        var sliderControlPoints = new Vector2[sliderValues.Count - 1];
        for (var i = 0; i < sliderControlPoints.Length; ++i)
        {
            using var controlPointValues = sliderValues[i + 1].AsReadOnlySpan().Split([':']);
            sliderControlPoints[i] = new(float.Parse(controlPointValues[0].AsReadOnlySpan(), CultureInfo.InvariantCulture),
                float.Parse(controlPointValues[1].AsReadOnlySpan(), CultureInfo.InvariantCulture));

            foreach (var value in controlPointValues) value.Dispose();
        }

        var nodeCount = int.Parse(values[6].AsReadOnlySpan(), CultureInfo.InvariantCulture) + 1;
        var length = float.Parse(values[7].AsReadOnlySpan(), CultureInfo.InvariantCulture);

        var sliderMultiplierLessLength = length / beatmap.SliderMultiplier;
        var travelDurationBeats = sliderMultiplierLessLength / 100 * controlPoint.SliderMultiplier;
        var travelDuration = timingPoint.BeatDuration * travelDurationBeats;

        var sliderNodes = new OsuSliderNode[nodeCount];
        for (var i = 0; i < sliderNodes.Length; i++)
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

        if (values.Count > 8)
        {
            var sliderAddition = values[8];
            using var sliderAdditionValues = sliderAddition.AsReadOnlySpan().Split(['|']);
            for (var i = 0; i < sliderAdditionValues.Count; i++)
            {
                sliderNodes[i].Additions =
                    (HitSoundAddition)int.Parse(sliderAdditionValues[i].AsReadOnlySpan(), CultureInfo.InvariantCulture);

                sliderAdditionValues[i].Dispose();
            }
        }

        if (values.Count > 9)
        {
            var sampleAndAdditionSampleSet = values[9];
            using var sampleAndAdditionSampleSetValues = sampleAndAdditionSampleSet.AsReadOnlySpan().Split(['|']);
            for (var i = 0; i < sampleAndAdditionSampleSetValues.Count; i++)
            {
                var node = sliderNodes[i];

                using var sampleAndAdditionSampleSetValue = sampleAndAdditionSampleSetValues[i];
                using var sampleAndAdditionSampleSetValues2 = sampleAndAdditionSampleSetValue.AsReadOnlySpan().Split([':']);

                var nodeSampleSet = (SampleSet)int.Parse(sampleAndAdditionSampleSetValues2[0].AsReadOnlySpan(),
                    CultureInfo.InvariantCulture);

                var nodeAdditionsSampleSet = int.Parse(sampleAndAdditionSampleSetValues2[1].AsReadOnlySpan(),
                    CultureInfo.InvariantCulture);

                foreach (var value in sampleAndAdditionSampleSetValues2) value.Dispose();

                if (nodeSampleSet != 0)
                {
                    node.SampleSet = nodeSampleSet;
                    node.AdditionsSampleSet = nodeSampleSet;
                }

                if (nodeAdditionsSampleSet != 0) node.AdditionsSampleSet = (SampleSet)nodeAdditionsSampleSet;
            }
        }

        var samplePath = "";
        if (values.Count < 11)
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
        using var specialValues = special.AsReadOnlySpan().Split([':']);

        var objectSampleSet = (SampleSet)int.Parse(specialValues[0].AsReadOnlySpan(), CultureInfo.InvariantCulture);
        var objectAdditionsSampleSet = int.Parse(specialValues[1].AsReadOnlySpan(), CultureInfo.InvariantCulture);
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

/// <summary>Represents a slider node in an osu! slider.</summary>
public class OsuSliderNode
{
    /// <summary>The hit sound additions of this node.</summary>
    public HitSoundAddition Additions { get; set; }

    /// <summary>The custom sample set of this node.</summary>
    public int CustomSampleSet { get; set; }

    /// <summary>The sample set of this node.</summary>
    public SampleSet SampleSet { get; set; }

    /// <summary>The additions sample set of this node.</summary>
    public SampleSet AdditionsSampleSet { get; set; }

    /// <summary>The time in milliseconds of this node.</summary>
    public float Time { get; set; }

    /// <summary>The volume of this node.</summary>
    public float Volume { get; set; }
}

/// <summary>The curve type of a slider.</summary>
public enum SliderCurveType
{
    /// <summary>The curve type is unknown.</summary>
    Unknown,

    /// <summary>The curve is linear.</summary>
    Linear,

    /// <summary>The curve is a Catmull-Rom spline.</summary>
    Catmull,

    /// <summary>The curve is a bézier curve.</summary>
    Bezier,

    /// <summary>The curve is a perfect circular arc.</summary>
    Perfect
}