using System;
using System.Globalization;

namespace StorybrewCommon.Mapset;

///<summary> Represents a control point in osu! </summary>
[Serializable] public class ControlPoint : IComparable<ControlPoint>
{
    ///<summary> Returns a control point with default values. </summary>
    public static readonly ControlPoint Default = new();

    ///<summary> The offset, or time, of this control point. </summary>
    public double Offset { get; init; }

    ///<summary> Beats per measure, or bar, of this control point. </summary>
    public int BeatPerMeasure { get; init; } = 4;

    ///<summary> The default sample set of this control point. </summary>
    public SampleSet SampleSet { get; init; } = SampleSet.Normal;

    ///<summary> The custom sample set index of this control point. </summary>
    public int CustomSampleSet { get; init; }

    ///<summary> The object volume of this control point. </summary>
    public float Volume { get; init; } = 100;

    ///<summary> Whether this control point is inherited (is green line). </summary>
    public bool IsInherited { get; init; }

    ///<summary> Whether this control point has kiai enabled. </summary>
    public bool IsKiai { get; init; }

    ///<summary> Whether this control point has "Omit first bar line" enabled. </summary>
    public bool OmitFirstBarLine { get; init; }

    double beatDurationSV = 500;

    ///<returns> The duration of a beat based on the BPM measure of the control point. </returns>
    public double BeatDuration => IsInherited ? throw new InvalidOperationException("Control points don't have a beat duration, use timing points") : beatDurationSV;

    ///<summary> The beats per minute measure of this control point. </summary>
    public double BPM => BeatDuration == 0 ? 0 : 60000 / BeatDuration;

    ///<summary> The slider velocity multiplier of this control point. </summary>
    public double SliderMultiplier => beatDurationSV > 0 ? 1 : -(beatDurationSV / 100);

    ///<summary> Compares this control point to <paramref name="other"/>. </summary>
    public int CompareTo(ControlPoint other)
    {
        var value = (int)(Offset - other.Offset);
        return value != 0 ? value : (other.IsInherited ? 0 : 1) - (IsInherited ? 0 : 1);
    }

    ///<summary> Converts this control point to a <see cref="string"/> representation. </summary>
    public override string ToString() => (IsInherited ? $"{Offset}ms, {SliderMultiplier}x, {BeatPerMeasure}/4" : $"{Offset}ms, {BPM}BPM, {BeatPerMeasure}/4") + (IsKiai ? " Kiai" : "");

    ///<inheritdoc/>
    public override int GetHashCode() => ToString().GetHashCode();

    ///<summary> Parses a control point from a given line. </summary>
    public static ControlPoint Parse(string line)
    {
        var values = line.Split(',');
        if (values.Length < 2) throw new InvalidOperationException($"Control point has less than the 2 required parameters: {line}");

        return new()
        {
            Offset = double.Parse(values[0], CultureInfo.InvariantCulture),
            beatDurationSV = double.Parse(values[1], CultureInfo.InvariantCulture),
            BeatPerMeasure = values.Length > 2 ? int.Parse(values[2], CultureInfo.InvariantCulture) : 4,
            SampleSet = values.Length > 3 ? (SampleSet)int.Parse(values[3], CultureInfo.InvariantCulture) : SampleSet.Normal,
            CustomSampleSet = values.Length > 4 ? int.Parse(values[4], CultureInfo.InvariantCulture) : 0,
            Volume = values.Length > 5 ? int.Parse(values[5], CultureInfo.InvariantCulture) : 100,
            IsInherited = values.Length > 6 && int.Parse(values[6], CultureInfo.InvariantCulture) == 0,
            IsKiai = values.Length > 7 && (int.Parse(values[7], CultureInfo.InvariantCulture) & 1) != 0,
            OmitFirstBarLine = values.Length > 7 && (int.Parse(values[7], CultureInfo.InvariantCulture) & 8) != 0
        };
    }
}