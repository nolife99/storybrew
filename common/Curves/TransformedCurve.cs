using StorybrewCommon.Storyboarding.CommandValues;
using System;

namespace StorybrewCommon.Curves
{
    ///<summary> Represents any <see cref="Curves.Curve"/> that has been transformed. </summary>
    [Serializable] public class TransformedCurve : Curve
    {
        readonly Curve curve;
        readonly CommandPosition offset;
        readonly float scale;
        readonly bool reversed;

        ///<summary> The transformed curve. </summary>
        public Curve Curve => curve;

        ///<summary> Constructs a transformed curve from <paramref name="curve"/> and given transformations. </summary>
        public TransformedCurve(Curve curve, CommandPosition offset, float scale, bool reversed = false)
        {
            this.curve = curve;
            this.offset = offset;
            this.scale = scale;
            this.reversed = reversed;
        }

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
}