using StorybrewCommon.Storyboarding.CommandValues;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StorybrewCommon.Curves
{
    ///<summary> Represents a composite curve that is constructed from multiple curves. </summary>
    [Serializable] public class CompositeCurve : Curve
    {
        readonly Curve[] curves;

        ///<summary> Returns a readonly list of curves that makes up the composite curve. </summary>
        public IReadOnlyList<Curve> Curves => curves;

        ///<inheritdoc/>
        public CommandPosition StartPosition => curves[0].StartPosition;

        ///<inheritdoc/>
        public CommandPosition EndPosition => curves[curves.Length - 1].EndPosition;

        ///<inheritdoc/>
        public double Length
        {
            get
            {
                var length = 0d;
                for (var i = 0; i < curves.Length; ++i) length += curves[i].Length;
                return length;
            }
        }

        ///<summary> Constructs a composite curve from a list of curves <paramref name="curves"/>. </summary>
        public CompositeCurve(IEnumerable<Curve> curves) => this.curves = (curves as Curve[]) ?? curves.ToArray();

        ///<inheritdoc/>
        public CommandPosition PositionAtDistance(double distance)
        {
            for (var i = 0; i < curves.Length; ++i)
            {
                var curve = curves[i];
                if (distance < curve.Length) return curve.PositionAtDistance(distance);
                distance -= curve.Length;
            }
            return curves[curves.Length - 1].EndPosition;
        }

        ///<inheritdoc/>
        public CommandPosition PositionAtDelta(double delta)
        {
            var length = Length;

            var d = delta;
            for (var i = 0; i < curves.Length; ++i)
            {
                var curve = curves[i];
                var curveDelta = curve.Length / length;

                if (d < curveDelta) return curve.PositionAtDelta(d / curveDelta);
                d -= curveDelta;
            }
            return EndPosition;
        }
    }
}