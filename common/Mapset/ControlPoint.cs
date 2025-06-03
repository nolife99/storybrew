namespace StorybrewCommon.Mapset;

using System;
using System.Globalization;
using BrewLib.Util;
using Tiny.PooledCollections.Generic.StructBased.Internals;

///<summary> Represents a control point in an osu! beatmap. </summary>
public record ControlPoint : IComparable<ControlPoint>
{
    ///<summary> A control point with default values. </summary>
    public static readonly ControlPoint Default = new();

    float beatDurationSV = 500;

    ///<summary> The offset, or time, of this control point. </summary>
    public float Offset { get; private init; }

    ///<summary> Beats per measure, or bar, of this control point. </summary>
    public int BeatPerMeasure { get; private init; } = 4;

    ///<summary> The default sample set of this control point. </summary>
    public SampleSet SampleSet { get; private init; } = SampleSet.Normal;

    ///<summary> The custom sample set index of this control point. </summary>
    public int CustomSampleSet { get; private init; }

    ///<summary> The object volume of this control point. </summary>
    public float Volume { get; private init; }

    ///<summary> Whether this control point is inherited (is green line). </summary>
    public bool IsInherited { get; private init; }

    ///<summary> Whether this control point has kiai enabled. </summary>
    public bool IsKiai { get; private init; }

    ///<summary> Whether this control point has "Omit first bar line" enabled. </summary>
    public bool OmitFirstBarLine { get; private init; }

    ///<returns> The duration of a beat based on the BPM measure of the control point. </returns>
    public float BeatDuration => IsInherited ?
        throw new InvalidOperationException("Control points don't have a beat duration, use timing points") :
        beatDurationSV;

    ///<summary> The beats per minute measure of this control point. </summary>
    public float BPM => BeatDuration == 0 ? 0 : 60000 / BeatDuration;

    ///<summary> The slider velocity multiplier of this control point. </summary>
    public float SliderMultiplier => beatDurationSV > 0 ? 1 : -(beatDurationSV / 100);

    /// <summary> Compares this control point to <paramref name="other"/>. </summary>
    public int CompareTo(ControlPoint other)
    {
        var value = (int)(Offset - other.Offset);
        return value != 0 ? value : (other.IsInherited ? 0 : 1) - (IsInherited ? 0 : 1);
    }

    /// <inheritdoc/>
    public override string ToString()
        => (IsInherited ?
                $"{Offset}ms, {SliderMultiplier}x, {BeatPerMeasure}/4" :
                $"{Offset}ms, {BPM}BPM, {BeatPerMeasure}/4") +
            (IsKiai ? " Kiai" : "");

    /// <inheritdoc/>
    public override int GetHashCode() => ToString().GetHashCode();

    ///<summary> Parses a control point from a given line. </summary>
    public static ControlPoint Parse(ReadOnlySpan<char> line)
    {
        using var values = line.Split([',']);
        if (values.Count < 2)
            throw new InvalidOperationException($"Control point has less than the 2 required parameters: {line}");

        ControlPoint result = new()
        {
            Offset = float.Parse(values[0].AsReadOnlySpan(), CultureInfo.InvariantCulture),
            beatDurationSV = float.Parse(values[1].AsReadOnlySpan(), CultureInfo.InvariantCulture),
            BeatPerMeasure = values.Count > 2 ? int.Parse(values[2].AsReadOnlySpan(), CultureInfo.InvariantCulture) : 4,
            SampleSet =
                values.Count > 3 ?
                    (SampleSet)int.Parse(values[3].AsReadOnlySpan(), CultureInfo.InvariantCulture) :
                    SampleSet.Normal,
            CustomSampleSet = values.Count > 4 ? int.Parse(values[4].AsReadOnlySpan(), CultureInfo.InvariantCulture) : 0,
            Volume = values.Count > 5 ? int.Parse(values[5].AsReadOnlySpan(), CultureInfo.InvariantCulture) : 100,
            IsInherited = values.Count > 6 && int.Parse(values[6].AsReadOnlySpan(), CultureInfo.InvariantCulture) == 0,
            IsKiai = values.Count > 7 && (int.Parse(values[7].AsReadOnlySpan(), CultureInfo.InvariantCulture) & 1) != 0,
            OmitFirstBarLine = values.Count > 7 &&
                (int.Parse(values[7].AsReadOnlySpan(), CultureInfo.InvariantCulture) & 8) != 0
        };

        foreach (var value in values) value.Dispose();

        return result;
    }
}