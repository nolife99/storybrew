using System;
using StorybrewCommon.Storyboarding.CommandValues;

namespace StorybrewCommon.Curves;

///<summary> Represents any <see cref="Curves.Curve"/> that has been transformed. </summary>
///<remarks> Constructs a transformed curve from <paramref name="curve"/> and given transformations. </remarks>
[Serializable] public class TransformedCurve(Curve curve, CommandPosition offset, float scale, bool reversed = false) : Curve
{
    ///<summary> The transformed curve. </summary>
    public Curve Curve => curve;

    ///<inheritdoc/>
    public CommandPosition StartPosition => (reversed ? curve.EndPosition : curve.StartPosition) * scale + offset;

    ///<inheritdoc/>
    public CommandPosition EndPosition => (reversed ? curve.StartPosition : curve.EndPosition) * scale + offset;

    ///<inheritdoc/>
    public double Length => curve.Length * scale;

    ///<inheritdoc/>
    public CommandPosition PositionAtDistance(double distance) => curve.PositionAtDistance(reversed ? curve.Length - distance : distance) * scale + offset;
    
    ///<inheritdoc/>
    public CommandPosition PositionAtDelta(double delta) => curve.PositionAtDelta(reversed ? 1.0 - delta : delta) * scale + offset;
}